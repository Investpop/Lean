﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE80;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace QuantConnect.VisualStudioPlugin
{
    /// <summary>
    /// Command handler for QuantConnect Solution Explorer menu buttons
    /// </summary>
    internal sealed class SolutionExplorerMenuCommand
    {
        /// <summary>
        /// Command IDs for Solution Explorer menu buttons
        /// </summary>
        public const int SendForBacktestingCommandId = 0x0100;
        public const int SaveToQuantConnectCommandId = 0x0110;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("00ce2ccb-74c7-42f4-bf63-52c573fc1532");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly QuantConnectPackage _package;

        private DTE2 _dte2;

        private ProjectFinder _projectFinder;

        private LogInCommand _logInCommand;

        /// <summary>
        /// Initializes a new instance of the <see cref="SolutionExplorerMenuCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private SolutionExplorerMenuCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            _package = package as QuantConnectPackage;
            _dte2 = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            _logInCommand = CreateLogInCommand();

            var commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                RegisterSendForBacktesting(commandService);
                RegisterSaveToQuantConnect(commandService);
            }
        }


        private ProjectFinder CreateProjectFinder()
        {
            return new ProjectFinder(PathUtils.GetSolutionFolder(_dte2));
        }

        private LogInCommand CreateLogInCommand()
        {
            return new LogInCommand();
        }

        private void RegisterSendForBacktesting(OleMenuCommandService commandService)
        {
            var menuCommandID = new CommandID(CommandSet, SendForBacktestingCommandId);
            var oleMenuItem = new OleMenuCommand(new EventHandler(SendForBacktestingCallback), menuCommandID);
            commandService.AddCommand(oleMenuItem);
        }

        private void RegisterSaveToQuantConnect(OleMenuCommandService commandService)
        {
            var menuCommandID = new CommandID(CommandSet, SaveToQuantConnectCommandId);
            var oleMenuItem = new OleMenuCommand(new EventHandler(SaveToQuantConnectCallback), menuCommandID);
            commandService.AddCommand(oleMenuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static SolutionExplorerMenuCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return _package;
            }
        }

        // Lazily create ProjectFinder only when we have an opened solution
        private ProjectFinder ProjectFinder
        {
            get {
                if (_projectFinder == null)
                {
                    _projectFinder = CreateProjectFinder();
                }
                return _projectFinder;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new SolutionExplorerMenuCommand(package);
        }

        private void SendForBacktestingCallback(object sender, EventArgs e)
        {
            ExecuteOnProject(sender, (selectedProjectId, selectedProjectName, files) =>
            {
                var fileNames = files.Select(f => f.FileName).ToList();

                VsUtils.DisplayInStatusBar(this.ServiceProvider, "Uploading files to server ...");
                UploadFilesToServer(selectedProjectId, files);

                VsUtils.DisplayInStatusBar(this.ServiceProvider, "Compiling project ...");
                var compileStatus = CompileProjectOnServer(selectedProjectId);
                if (compileStatus.State == Api.CompileState.BuildError)
                {
                    VsUtils.DisplayInStatusBar(this.ServiceProvider, "Compile error.");
                    VsUtils.ShowMessageBox(this.ServiceProvider, "Compile Error", "Error when compiling project.");
                    return;
                }

                VsUtils.DisplayInStatusBar(this.ServiceProvider, "Backtesting project ...");
                Api.Backtest backtest = BacktestProjectOnServer(selectedProjectId, compileStatus.CompileId);
                // Errors are not being transfered in response, so client can't tell if the backtest failed or not.
                // This response error handling code will not work but should.
                /* if (backtest.Errors.Count != 0) {
                    VsUtils.DisplayInStatusBar(this.ServiceProvider, "Backtest error.");
                    showMessageBox("Backtest Error", "Error when backtesting project.");
                    return;
                }*/

                VsUtils.DisplayInStatusBar(this.ServiceProvider, "Backtest complete.");
                var projectUrl = string.Format(
                    CultureInfo.CurrentCulture, 
                    "https://www.quantconnect.com/terminal/#open/{0}", 
                    selectedProjectId
                );
                Process.Start(projectUrl);
            });
        }

        private void SaveToQuantConnectCallback(object sender, EventArgs e)
        {
            ExecuteOnProject(sender, (selectedProjectId, selectedProjectName, files) =>
            {
                var fileNames = files.Select(f => f.FileName).ToList();

                VsUtils.DisplayInStatusBar(this.ServiceProvider, "Uploading files to server ...");
                UploadFilesToServer(selectedProjectId, files);
                VsUtils.DisplayInStatusBar(this.ServiceProvider, "Files upload complete.");
            });
        }

        private void UploadFilesToServer(int selectedProjectId, List<SelectedItem> files)
        {
            var api = AuthorizationManager.GetInstance().GetApi();
            foreach (var file in files)
            {
                api.DeleteProjectFile(selectedProjectId, file.FileName);
                var fileContent = File.ReadAllText(file.FilePath);
                api.AddProjectFile(selectedProjectId, file.FileName, fileContent);
            }
        }

        private Api.Compile CompileProjectOnServer(int projectId)
        {
            var api = AuthorizationManager.GetInstance().GetApi();
            var compileStatus = api.CreateCompile(projectId);
            var compileId = compileStatus.CompileId;
            while (compileStatus.State == Api.CompileState.InQueue)
            {
                Thread.Sleep(5000);
                compileStatus = api.ReadCompile(projectId, compileId);
            }
            return compileStatus;
        }

        private Api.Backtest BacktestProjectOnServer(int projectId, string compileId)
        {
            var api = AuthorizationManager.GetInstance().GetApi();
            var backtestStatus = api.CreateBacktest(projectId, compileId, "My New Backtest");
            var backtestId = backtestStatus.BacktestId;
            while (!backtestStatus.Completed)
            {
                Thread.Sleep(5000);
                backtestStatus = api.ReadBacktest(projectId, backtestId);
            }
            return backtestStatus;
        }

        private void ExecuteOnProject(object sender, Action<int, string, List<SelectedItem>> onProject)
        {
            if (_logInCommand.DoLogIn(this.ServiceProvider, _package.DataPath, explicitLogin: false))
            {
                var api = AuthorizationManager.GetInstance().GetApi();
                var projects = api.ListProjects().Projects;
                var projectNames = projects.Select(p => Tuple.Create(p.ProjectId, p.Name)).ToList();

                var files = GetSelectedFiles(sender);
                var fileNames = files.Select(tuple => tuple.FileName).ToList();
                var suggestedProjectName = ProjectFinder.ProjectNameForFiles(fileNames);
                var projectNameDialog = new ProjectNameDialog(projectNames, suggestedProjectName);
                VsUtils.DisplayDialogWindow(projectNameDialog);

                if (projectNameDialog.ProjectNameProvided())
                {
                    var selectedProjectName = projectNameDialog.GetSelectedProjectName();
                    var selectedProjectId = projectNameDialog.GetSelectedProjectId();
                    ProjectFinder.AssociateProjectWith(selectedProjectName, fileNames);

                    if (!selectedProjectId.HasValue)
                    {
                        var newProjectLanguage = PathUtils.DetermineProjectLanguage(files.Select(f => f.FilePath).ToList());
                        if (!newProjectLanguage.HasValue)
                        {
                            VsUtils.ShowMessageBox(this.ServiceProvider, "Failed to determine project language",
                                $"Failed to determine programming laguage for a project");
                            return;
                        }

                        selectedProjectId = CreateQuantConnectProject(selectedProjectName, newProjectLanguage.Value);
                        if (!selectedProjectId.HasValue)
                        {
                            VsUtils.ShowMessageBox(this.ServiceProvider, "Failed to create a project", $"Failed to create a project {selectedProjectName}");
                        }
                        onProject.Invoke(selectedProjectId.Value, selectedProjectName, files);
                    }
                    else
                    {
                        onProject.Invoke(selectedProjectId.Value, selectedProjectName, files);
                    }
                }
            }
        }

        private int? CreateQuantConnectProject(string projectName, Language projectLanguage)
        {
            var api = AuthorizationManager.GetInstance().GetApi();
            var projectResponse = api.CreateProject(projectName, projectLanguage);
            if (!projectResponse.Success)
            {
                return null;
            }
            return projectResponse.Projects[0].ProjectId;
        }

        private List<SelectedItem> GetSelectedFiles(object sender)
        {
            var myCommand = sender as OleMenuCommand;

            var selectedFiles = new List<SelectedItem>();
            var selectedItems = (object[])_dte2.ToolWindows.SolutionExplorer.SelectedItems;
            foreach (EnvDTE.UIHierarchyItem selectedUIHierarchyItem in selectedItems)
            {
                if (selectedUIHierarchyItem.Object is EnvDTE.ProjectItem)
                {
                    var item = selectedUIHierarchyItem.Object as EnvDTE.ProjectItem;
                    var filePath = item.Properties.Item("FullPath").Value.ToString();
                    var selectedItem = new SelectedItem
                    {
                        FileName = item.Name,
                        FilePath = filePath
                    };
                    selectedFiles.Add(selectedItem);
                }
            }

            // Check if the user selected a folder, and include files in directories otherwise language inference breaks.
            // Also to maintain child folder structure on webclient tweak the filename to contain folders expressed in URI format.
            if (selectedFiles.Count == 1 &&
                string.IsNullOrEmpty(Path.GetExtension(selectedFiles.First().FilePath)))
            {
                var basePath = selectedFiles.First().FilePath;
                var nonFolders = Directory.GetFiles(selectedFiles.First().FilePath, "*", SearchOption.AllDirectories);

                selectedFiles = nonFolders.Select(c => new SelectedItem
                {
                    FileName = "/" + c.Replace(basePath, string.Empty).Replace("\\", "/"),
                    FilePath = c
                }).ToList();
            }

            return selectedFiles;
        }
    }

    class SelectedItem
    {
        public string FileName
        {
            get; set;
        }

        public string FilePath
        {
            get; set;
        }
    }
}
