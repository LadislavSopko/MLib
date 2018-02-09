namespace FileListViewTest.ViewModels
{
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using FileListView.Interfaces;
    using FileListViewTest.Command;
    using FileListViewTest.Interfaces;
    using FileSystemModels;
    using FileSystemModels.Browse;
    using FileSystemModels.Events;
    using FileSystemModels.Interfaces;
    using FileSystemModels.Interfaces.Bookmark;
    using FileSystemModels.Models;
    using FilterControlsLib.Interfaces;
    using FolderControlsLib.Interfaces;

    /// <summary>
    /// Class implements a folder/file view model class
    /// that can be used to dispaly filesystem related content in an ItemsControl.
    /// </summary>
    internal class ListControllerViewModel : Base.ViewModelBase, IListControllerViewModel
    {
        #region fields
        private string _SelectedFolder = string.Empty;
        private object _LockObject = new object();
        private ICommand mRefreshCommand;
        #endregion fields

        #region constructor
        /// <summary>
        /// Custom class constructor
        /// </summary>
        /// <param name="onFileOpenMethod"></param>
        public ListControllerViewModel(System.EventHandler<FileOpenEventArgs> onFileOpenMethod)
          : this()
        {
            // Remove the standard constructor event that is fired when a user opens a file
            this.FolderItemsView.OnFileOpen -= this.FolderItemsView_OnFileOpen;

            // ...and establish a new link (if any)
            if (onFileOpenMethod != null)
                this.FolderItemsView.OnFileOpen += onFileOpenMethod;
        }

        /// <summary>
        /// Class constructor
        /// </summary>
        public ListControllerViewModel()
        {
            FolderItemsView = FileListView.Factory.CreateFileListViewModel(new BrowseNavigation());
            FolderTextPath = FolderControlsLib.Factory.CreateFolderComboBoxVM();
            RecentFolders = FileSystemModels.Factory.CreateBookmarksViewModel();

            // This is fired when the user selects a new folder bookmark from the drop down button
            RecentFolders.BrowseEvent += FolderTextPath_BrowseEvent;

            // This is fired when the text path in the combobox changes to another existing folder
            FolderTextPath.BrowseEvent += FolderTextPath_BrowseEvent;

            Filters = FilterControlsLib.Factory.CreateFilterComboBoxViewModel();
            Filters.OnFilterChanged += this.FileViewFilter_Changed;

            // This is fired when the current folder in the listview changes to another existing folder
            this.FolderItemsView.BrowseEvent += FolderTextPath_BrowseEvent;

            // This is fired when the user requests to add a folder into the list of recently visited folders
            this.FolderItemsView.BookmarkFolder.RequestEditBookmarkedFolders += this.FolderItemsView_RequestEditBookmarkedFolders;

            // This event is fired when a user opens a file
            this.FolderItemsView.OnFileOpen += this.FolderItemsView_OnFileOpen;
        }
        #endregion constructor

        #region properties
        /// <summary>
        /// Gets the command that updates the currently viewed
        /// list of directory items (files and sub-directories).
        /// </summary>
        public ICommand RefreshCommand
        {
            get
            {
                if (this.mRefreshCommand == null)
                    this.mRefreshCommand = new RelayCommand<object>
                        (async (p) =>
                        {
                            await RefreshCommand_ExecutedAsync(p as string);
                        });

                return this.mRefreshCommand;
            }
        }

        /// <summary>
        /// Gets the currently selected recent location string (if any) or null.
        /// </summary>
        public string SelectedRecentLocation
        {
            get
            {
                if (this.RecentFolders != null)
                {
                    if (this.RecentFolders.SelectedItem != null)
                        return this.RecentFolders.SelectedItem.FullPath;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the viewmodel that exposes recently visited locations (bookmarked folders).
        /// </summary>
        public IBookmarksViewModel RecentFolders { get; }

        /// <summary>
        /// Expose a viewmodel that can represent a Folder-ComboBox drop down
        /// element similar to a web browser Uri drop down control but using
        /// local paths only.
        /// </summary>
        public IFolderComboBoxViewModel FolderTextPath { get; }

        /// <summary>
        /// Expose a viewmodel that can represent a Filter-ComboBox drop down
        /// similar to the top-right filter/search combo box in Windows Exploer windows.
        /// </summary>
        public IFilterComboBoxViewModel Filters { get; }

        /// <summary>
        /// Expose a viewmodel that can support a listview showing folders and files
        /// with their system specific icon.
        /// </summary>
        public IFileListViewModel FolderItemsView { get; }

        /// <summary>
        /// Gets the currently selected folder path string.
        /// </summary>
        public string SelectedFolder
        {
            get
            {
                return this._SelectedFolder;
            }

            private set
            {
                if (this._SelectedFolder != value)
                {
                    this._SelectedFolder = value;
                    base.NotifyPropertyChanged(() => this.SelectedFolder);
                }
            }
        }
        #endregion properties

        #region methods
        #region Explorer settings model
        /// <summary>
        /// Configure this viewmodel (+ attached browser viewmodel) with the given settings and
        /// initialize viewmodels with UserProfile.CurrentPath location.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        bool IConfigExplorerSettings.ConfigureExplorerSettings(ExplorerSettingsModel settings)
        {
            if (settings == null)
                return false;

            if (settings.UserProfile == null)
                return false;

            if (settings.UserProfile.CurrentPath == null)
                return false;

            try
            {
                // Set currently viewed folder in Explorer Tool Window
                var t = new Task(async () =>
                {
                    await this.NavigateToFolderAsync(settings.UserProfile.CurrentPath, null);
                });

                t.RunSynchronously();

                this.Filters.ClearFilter();

                // Set file filter in file/folder list view
                foreach (var item in settings.FilterCollection)
                    this.Filters.AddFilter(item.FilterDisplayName, item.FilterText);

                // Add a current filter setting (if any is available)
                if (settings.UserProfile.CurrentFilter != null)
                {
                    this.Filters.SetCurrentFilter(settings.UserProfile.CurrentFilter.FilterDisplayName,
                                                  settings.UserProfile.CurrentFilter.FilterText);
                }

                this.RecentFolders.ClearFolderCollection();

                // Set collection of recent folder locations
                foreach (var item in settings.RecentFolders)
                    this.RecentFolders.AddFolder(item);

                this.FolderItemsView.SetShowIcons(settings.ShowIcons);
                this.FolderItemsView.SetIsFolderVisible(settings.ShowFolders);
                this.FolderItemsView.SetShowHidden(settings.ShowHiddenFiles);
                this.FolderItemsView.SetIsFiltered(settings.IsFiltered);
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Compare given <paramref name="input"/> settings with current settings
        /// and return a new settings model if the current settings
        /// changed in comparison to the given <paramref name="input"/> settings.
        /// 
        /// Always return current settings if <paramref name="input"/> settings is null.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        ExplorerSettingsModel IConfigExplorerSettings.GetExplorerSettings(ExplorerSettingsModel input)
        {
            var settings = new ExplorerSettingsModel();

            try
            {
                settings.UserProfile.SetCurrentPath(this.SelectedFolder);

                foreach (var item in settings.FilterCollection)
                {
                    if (item == settings.UserProfile.CurrentFilter)
                        this.AddFilter(item.FilterDisplayName, item.FilterText, true);
                    else
                        this.AddFilter(item.FilterDisplayName, item.FilterText);
                }

                foreach (var item in this.Filters.CurrentItems)
                {
                    if (item == this.Filters.SelectedItem)
                        settings.AddFilter(item.FilterDisplayName, item.FilterText, true);
                    else
                        settings.AddFilter(item.FilterDisplayName, item.FilterText);
                }

                foreach (var item in this.RecentFolders.DropDownItems)
                    settings.AddRecentFolder(item.FullPath);

                if (string.IsNullOrEmpty(this.SelectedRecentLocation) == false)
                {
                    settings.LastSelectedRecentFolder = this.SelectedRecentLocation;
                    settings.AddRecentFolder(this.SelectedRecentLocation);
                }

                settings.ShowIcons = this.FolderItemsView.ShowIcons;
                settings.ShowFolders = this.FolderItemsView.ShowFolders;
                settings.ShowHiddenFiles = this.FolderItemsView.ShowHidden;
                settings.IsFiltered = this.FolderItemsView.IsFiltered;

                if (ExplorerSettingsModel.CompareSettings(input, settings) == false)
                    return settings;
                else
                    return null;
            }
            catch
            {
                throw;
            }
        }
        #endregion Explorer settings model

        #region Bookmarked Folders Methods
        /// <summary>
        /// Add a recent folder location into the collection of recent folders.
        /// This collection can then be used in the folder combobox drop down
        /// list to store user specific customized folder short-cuts.
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="selectNewFolder"></param>
        public void AddRecentFolder(string folderPath, bool selectNewFolder = false)
        {
            this.RecentFolders.AddFolder(folderPath, selectNewFolder);
        }

        /// <summary>
        /// Removes a recent folder location into the collection of recent folders.
        /// This collection can then be used in the folder combobox drop down
        /// list to store user specific customized folder short-cuts.
        /// </summary>
        /// <param name="path"></param>
        public void RemoveRecentFolder(string path)
        {
            if (string.IsNullOrEmpty(path) == true)
                return;

            this.RecentFolders.RemoveFolder(path);
        }

        /// <summary>
        /// Copies all of the given bookmars into the destionations bookmarks collection.
        /// </summary>
        /// <param name="srcRecentFolders"></param>
        /// <param name="dstRecentFolders"></param>
        public void CloneBookmarks(IBookmarksViewModel srcRecentFolders,
                                   IBookmarksViewModel dstRecentFolders)
        {
            if (srcRecentFolders == null || dstRecentFolders == null)
                return;

            dstRecentFolders.ClearFolderCollection();

            // Set collection of recent folder locations
            foreach (var item in srcRecentFolders.DropDownItems)
                dstRecentFolders.AddFolder(item.FullPath);
        }
        #endregion Bookmarked Folders Methods

        #region Change filter methods
        /// <summary>
        /// Add a new filter argument to the list of filters and
        /// select this filter if <paramref name="bSelectNewFilter"/>
        /// indicates it.
        /// </summary>
        /// <param name="filterString"></param>
        /// <param name="bSelectNewFilter"></param>
        public void AddFilter(string filterString,
                              bool bSelectNewFilter = false)
        {
            this.Filters.AddFilter(filterString, bSelectNewFilter);
        }

        /// <summary>
        /// Add a new filter argument to the list of filters and
        /// select this filter if <paramref name="bSelectNewFilter"/>
        /// indicates it.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="filterString"></param>
        /// <param name="bSelectNewFilter"></param>
        public void AddFilter(string name, string filterString,
                              bool bSelectNewFilter = false)
        {
            this.Filters.AddFilter(name, filterString, bSelectNewFilter);
        }
        #endregion Change filter methods

        /// <summary>
        /// Master controller interface method to navigate all views
        /// to the folder indicated in <paramref name="folder"/>
        /// - updates all related viewmodels.
        /// </summary>
        /// <param name="itemPath"></param>
        /// <param name="requestor"</param>
        public void NavigateToFolder(IPathModel itemPath)
        {
            var t = new Task(async () => { await NavigateToFolderAsync(itemPath, null); });

            t.RunSynchronously();
        }

        /// <summary>
        /// Master controler interface method to navigate all views
        /// to the folder indicated in <paramref name="folder"/>
        /// - updates all related viewmodels.
        /// </summary>
        /// <param name="itemPath"></param>
        /// <param name="requestor"</param>
        private async Task NavigateToFolderAsync(IPathModel itemPath, object sender)
        {
            try
            {
                FolderItemsView.SetExternalBrowsingState(true);
                FolderTextPath.SetExternalBrowsingState(true);
                SelectedFolder = itemPath.Path;

                if (FolderTextPath != sender)
                {
                    // Navigate Folder ComboBox to this folder
                    await FolderTextPath.NavigateToAsync(itemPath);
                }

                if (FolderItemsView != sender)
                {
                    // Navigate Folder/File ListView to this folder
                    await FolderItemsView.NavigateToAsync(itemPath);
                }
            }
            catch{}
            finally
            {
                FolderItemsView.SetExternalBrowsingState(false);
                FolderTextPath.SetExternalBrowsingState(false);
            }
        }

        /// <summary>
        /// Method executes when the user requests via UI bound command
        /// to refresh the currently displayed list of items.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private async Task RefreshCommand_ExecutedAsync(string path = null)
        {
            try
            {
                IPathModel location = null;
                if (path != null)
                    location = PathFactory.Create(path);
                else
                    location = PathFactory.Create(FolderTextPath.CurrentFolder);

                await NavigateToFolderAsync(location, null);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Executes when the file open event is fired and class was constructed with statndard constructor.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void FolderItemsView_OnFileOpen(object sender,
                                                  FileSystemModels.Events.FileOpenEventArgs e)
        {
            MessageBox.Show("File Open:" + e.FileName);
        }

        /// <summary>
        /// Applies the file/directory filter from the combobox on the listview entries.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileViewFilter_Changed(object sender, FileSystemModels.Events.FilterChangedEventArgs e)
        {
            this.FolderItemsView.ApplyFilter(e.FilterText);
        }

        /// <summary>
        /// The list view of folders and files requests to add or remove a folder
        /// to/from the collection of recent folders.
        /// -> Forward event to <seealso cref="FolderComboBoxViewModel"/> who manages
        /// the actual list of recent folders.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FolderItemsView_RequestEditBookmarkedFolders(object sender, EditBookmarkEvent e)
        {
            switch (e.Action)
            {
                case EditBookmarkEvent.RecentFolderAction.Remove:
                    this.RecentFolders.RemoveFolder(e.Folder);
                    break;

                case EditBookmarkEvent.RecentFolderAction.Add:
                    this.RecentFolders.AddFolder(e.Folder.Path);
                    break;

                default:
                    break;
            }
        }

        private void FolderTextPath_BrowseEvent(object sender,
                                                FileSystemModels.Browse.BrowsingEventArgs e)
        {
            var itemPath = e.Location;

            SelectedFolder = itemPath.Path;

            if (e.IsBrowsing == false && e.Result == BrowseResult.Complete)
            {
                if (FolderTextPath != sender)
                {
                    // Navigate Folder ComboBox to this folder
                    FolderTextPath.NavigateTo(itemPath);
                }

                if (FolderItemsView != sender)
                {
                    // Navigate Folder/File ListView to this folder
                    FolderItemsView.NavigateTo(itemPath);
                }
            }
            else
            {
                if (e.IsBrowsing == true)
                {
                    if (FolderTextPath != sender)
                    {
                        // Navigate Folder ComboBox to this folder
                        FolderTextPath.SetExternalBrowsingState(true);
                    }

                    if (FolderItemsView != sender)
                    {
                        // Navigate Folder/File ListView to this folder
                        FolderItemsView.SetExternalBrowsingState(true);
                    }
                }
            }
        }
        #endregion methods
    }
}