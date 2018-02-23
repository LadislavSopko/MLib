namespace FolderControlsLib.ViewModels
{
    using System;
    using System.IO;
    using FileSystemModels;
    using FileSystemModels.Interfaces;
    using FileSystemModels.Models.FSItems.Base;
    using FileSystemModels.Utils;
    using FolderControlsLib.Interfaces;
    using InplaceEditBoxLib.ViewModels;

    /// <summary>
    /// The Viewmodel for file system items
    /// </summary>
    internal class FolderItemViewModel : EditInPlaceViewModel, IFolderItemViewModel
    {
        #region fields
        /// <summary>
        /// Logger facility
        /// </summary>
        protected new static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private string _ItemName;
        private IPathModel _PathObject;
        #endregion fields

        #region constructor
        /// <summary>
        /// class constructor
        /// </summary>
        /// <param name="curdir"></param>
        /// <param name="displayName"></param>
        /// <param name="itemType"></param>
        /// <param name="showIcon"></param>
        /// <param name="indentation"></param>
        public FolderItemViewModel(string curdir,
                        FSItemType itemType,
                        string displayName,
                        bool showIcon)
          : this(curdir, itemType, displayName)
        {
            this.ShowIcon = showIcon;
        }

        /// <summary>
        /// class constructor
        /// </summary>
        /// <param name="curdir"></param>
        /// <param name="itemType"></param>
        /// <param name="itemName"></param>
        /// <param name="indentation"></param>
        public FolderItemViewModel(string curdir,
                        FSItemType itemType,
                        string itemName)
          : this()
        {
            this._PathObject = PathFactory.Create(curdir, itemType);
            this.ItemName = itemName;
        }

        /// <summary>
        /// class constructor
        /// </summary>
        /// <param name="model"></param>
        /// <param name="itemName"></param>
        /// <param name="indentation"></param>
        public FolderItemViewModel(IPathModel model,
                        string itemName,
                        bool isReadOnly = false)
            : this()
        {
            _PathObject = model.Clone() as IPathModel;
            ItemName = itemName;
            IsReadOnly = isReadOnly;
        }

        /// <summary>
        /// Hidden standard class constructor
        /// </summary>
        protected FolderItemViewModel()
        {
            _PathObject = null;

            ShowIcon = true;
        }
        #endregion constructor

        #region properties
        /// <summary>
        /// Gets the type (folder, file) of this item
        /// </summary>
        public FSItemType ItemType
        {
            get
            {
                return (this._PathObject != null ? this._PathObject.PathType : FSItemType.Unknown);
            }
        }

        /// <summary>
        /// Gets the path to this item
        /// </summary>
        public string ItemPath
        {
            get
            {
                return (this._PathObject != null ? this._PathObject.Path : null);
            }
        }

        /// <summary>
        /// Gets whether this folder is currently expanded or not.
        /// 
        /// This viewmodel, currently, has no use case for an expanded item.
        /// Therefore, this property returns a constanst false value.
        /// </summary>
        public bool IsExpanded { get { return false; } }

        /// <summary>
        /// Gets a name that can be used for display
        /// (is not necessarily the same as path)
        /// </summary>
        public string ItemName
        {
            get
            {
                return this._ItemName;
            }

            protected set
            {
                if (this._ItemName != value)
                {
                    this._ItemName = value;
                    this.RaisePropertyChanged(() => this.ItemName);
                }
            }
        }

        /// <summary>
        /// Gets a folder item string for display purposes.
        /// This string can evaluete to 'C:\ (Windows)' for drives,
        /// if the 'C:\' drive was named 'Windows'.
        /// </summary>
        public string ItemDisplayString
        {
            get
            {
                return IItemExtension.GetDisplayString(this as IItem);
            }
        }

        /// <summary>
        /// Determine whether a given path is an exeisting directory or not.
        /// </summary>
        /// <returns>true if this directory exists and otherwise false</returns>
        public bool DirectoryPathExists()
        {
            return this._PathObject.DirectoryPathExists();
        }

        /// <summary>
        /// Gets whether or not to show a tooltip for this item.
        /// </summary>
        public bool ShowIcon { get; private set; }

        /// <summary>
        /// Gets a copy of the internal <seealso cref="PathModel"/> object.
        /// </summary>
        public IPathModel GetModel
        {
            get
            {
                return this._PathObject.Clone() as IPathModel;
            }
        }
        #endregion properties

        #region methods
        /// <summary>
        /// Standard method to display contents of this class.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this.ItemPath;
        }

        /// <summary>
        /// Sets the display name of this item.
        /// </summary>
        /// <param name="stringToDisplay"></param>
        internal void SetDisplayName(string stringToDisplay)
        {
            ItemName = stringToDisplay;
        }
        #endregion methods
    }
}
