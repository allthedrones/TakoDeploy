﻿using Squirrel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TakoDeployCore;
using TakoDeployCore.Model;
using TakoDeployWPF.Domain;

namespace TakoDeployWPF
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel DataContextModel { get { return DataContext as MainViewModel; } }
        public MainWindow()
        {
            InitializeComponent();
#if !DEBUG
            AppDomain.CurrentDomain.ProcessExit += DisposeUpdateManager;
#endif
            this.DataContext = new MainViewModel();
            this.Loaded += MainWindow_Loaded;
            this.SizeChanged += MainWindow_SizeChanged;
           
            Console.WriteLine("Main Window Ctor");

        }

      

        #region SquirrelSuff
#if !DEBUG
        private static int _isUpdateManagerDisposed = 1;
        internal void DisposeUpdateManager(object sender, EventArgs e)
        {
            WaitForCheckForUpdateLockAcquire();

            if (1 == Interlocked.Exchange(ref _isUpdateManagerDisposed, 0))
            {
                UpdateManager.Dispose();
            }
        }

        private static void WaitForCheckForUpdateLockAcquire()
        {
            var goTime = _lastUpdateCheckDateTime + TimeSpan.FromMilliseconds(2000);
            var timeToWait = goTime - DateTime.Now;
            if (timeToWait > TimeSpan.Zero)
                Thread.Sleep(timeToWait);
        }
        private static DateTime _lastUpdateCheckDateTime = DateTime.Now - TimeSpan.FromDays(1);
        private readonly UpdateManager UpdateManager = new UpdateManager(updatepath);
        private static string updatepath = @"C:\Proyectos Web\Main\TakoDeploy\Source\Squirrel\Releases";

        private async Task<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates)
        {
            _lastUpdateCheckDateTime = DateTime.Now;
            return await UpdateManager.CheckForUpdate(ignoreDeltaUpdates);
        }
#endif
        #endregion Squirrel


        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DataContextModel.WindowSize = e.NewSize;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
#if !DEBUG

            SquirrelAwareApp.HandleEvents(onFirstRun: () => DataContextModel.ShowTheWelcomeWizard = true);
            UpdateManager.UpdateApp(progress =>
            {
                
            });
            //}
#endif

            // ItemToContextMenuConverter.FirstLevelContextMenu = this.Resources["FirstLevelContextMenu"] as ContextMenu;
            //  ItemToContextMenuConverter.SecondLevelContextMenu = this.Resources["SecondLevelContextMenu"] as ContextMenu;
        }

        private void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataContextModel.TreeSelectedItem = MainTreeView.SelectedItem;
        }

        private void MainTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            DataContextModel.TreeSelectedItem = MainTreeView.SelectedItem;
        }
    }
    public class MainViewModel : INotifyPropertyChanged
    {
        private List<object> treeViewData;
        public MainViewModel()
        {
            treeViewData = new List<object>() {
                    new TreeItemSourcesDataContext(),
                    new TreeItemScriptDataContext()
                };
            DocumentManager.OnNewDocument += DocumentManager_OnNewDocument;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void DocumentManager_OnNewDocument(object sender, EventArgs e)
        {
            DocumentManager.Current.DeploymentEvent += Current_DeploymentEvent;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TreeViewData"));
        }

        private void Current_DeploymentEvent(object sender, TakoDeployLib.Model.ProgressEventArgs e)
        {
            if (e.Exception != null)
            {
                var ex2 = e.Exception;
                if (ex2.InnerException != null)
                    ex2 = ex2.InnerException;
                (Microsoft.HockeyApp.HockeyClient.Current as Microsoft.HockeyApp.HockeyClient).HandleException(ex2);
            }
        }

        public ICommand RunNewSourceDialogCommand => new ButtonCommand(ExecuteRunDialog, CanExecuteDocumentIsPresent);
        public ICommand RunNewScriptDialogCommand => new ButtonCommand(ExecuteNewScriptDialog, CanExecuteDocumentIsPresent);
        public ICommand RunValidateCommand => new ButtonCommand(ExecuteValidate, CanExecuteDocumentIsPresent);
        public ICommand RunDeployCommand => new ButtonCommand(ExecuteRunDeployCommand, CanExecuteDeployment);

        public ICommand RunEditSelectedItemCommand => new ButtonCommand(ExecuteEditSelectedItemCommand, IsTreeItemSelected);
        public ICommand RunDeleteSelectedItemCommand => new ButtonCommand(ExecuteDeleteSelectedItemCommand, IsTreeItemSelected);

        public ICommand RunNewDocumentCommand => new ButtonCommand(ExecuteNewDocumentCommand);
        public ICommand RunOpenDocumentCommand => new ButtonCommand(ExecuteOpenDocumentCommand);
        public ICommand RunSaveDocumentCommand => new ButtonCommand(ExecuteSaveDocumentCommand, CanExecuteDocumentIsPresent);

        public List<object> TreeViewData
        {
            get
            {
                return treeViewData;
            }
        }

        public object TreeSelectedItem
        {
            get;
            set;
        }

        private Size _windowSize;
        public Size WindowSize
        {
            get
            {
                return _windowSize;
            }
            internal set
            {
                _windowSize = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("WindowSize"));
            }
        }

        public bool ShowTheWelcomeWizard
        {
            set
            {
                if (!value) return;
                var view = new WelcomeScreen();
                var result = MaterialDesignThemes.Wpf.DialogHost.Show(view, "RootDialog");
            }
        }

        private async void ExecuteEditSelectedItemCommand(object o)
        {
            var editSource = new SourceDatabase((SourceDatabase)TreeSelectedItem);
            var view = new Domain.SourceEditorDialog
            {
                DataContext = new SourceEditorViewModel() { Source = editSource }
            };
            view.Width = 450;
            view.Height = 750;

            //show the dialog
            var result = await MaterialDesignThemes.Wpf.DialogHost.Show(view, "RootDialog");
            if (result is bool)
            {
                if ((bool)result)
                {
                    ((SourceDatabase)TreeSelectedItem).CopyFrom(editSource);
                }
            }
        }
        private async void ExecuteDeleteSelectedItemCommand(object o)
        {
            var result = MessageBox.Show("Are you sure you want to delete this source?", "TakoDeploy", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            if (TreeSelectedItem is SourceDatabase)
            {
                DocumentManager.Current.Deployment.Sources.Remove((SourceDatabase)TreeSelectedItem);
            }
        }


        private async void ExecuteRunDialog(object o)
        {
            var source = new SourceDatabase();
            source.ConnectionString = @"Data Source=des.wgm.es\DES_2014;Initial Catalog=Pruebas_1 ;User ID=wgm ;Password=cafeina";
            source.ProviderName = "System.Data.SqlClient";
            source.Type = SourceType.DataSource;

            var view = new Domain.SourceEditorDialog
            {
                DataContext = new SourceEditorViewModel() { Source = source }
            };
            view.Width = 480;
            view.Height = 600;

            //show the dialog
            var result = await MaterialDesignThemes.Wpf.DialogHost.Show(view, "RootDialog", ClosingEventHandler);
            if (result is bool)
            {
                if ((bool)result)
                {
                    DocumentManager.Current.Deployment.Sources.Add(source);
                }

                //check the result...
                Console.WriteLine("Dialog was closed, the CommandParameter used to close it was: " + (result ?? "NULL"));
            }
        }

        private async void ExecuteNewScriptDialog(object o)
        {
            var script = new SqlScriptFile();
            var basename = "SqlScript";
            script.Name = basename + (DocumentManager.Current.Deployment.ScriptFiles.Where(x => x.Name.StartsWith(basename)).Count() + 1).ToString();
            var view = new ScriptEditor
            {
                DataContext = new ScriptEditorViewModel() { Script = script }
            };
            view.Width = WindowSize.Width;
            view.Height = WindowSize.Height;

            var result = await MaterialDesignThemes.Wpf.DialogHost.Show(view, "RootDialog", ClosingEventHandler);
            if (result is bool)
            {
                if ((bool)result)
                {
                    DocumentManager.Current.Deployment.ScriptFiles.Add(script);
                }

                //check the result...
                Console.WriteLine("Dialog was closed, the CommandParameter used to close it was: " + (result ?? "NULL"));
            }
        }

        private async void ExecuteNewDocumentCommand(object o)
        {
            if (DocumentManager.Current != null && DocumentManager.Current.IsModified)
            {
                var result = MessageBox.Show("There are changes in your current deployment document. Do you want yo save them?", "TakoDeploy", MessageBoxButton.YesNoCancel);
                switch (result)
                {

                    case MessageBoxResult.Yes:
                        ExecuteSaveDocumentCommand(o);
                        break;
                    case MessageBoxResult.No:
                        break;
                    default:
                        return;//cancel commmand
                }
            }
            DocumentManager.Current = new DocumentManager();
        }

        private async void ExecuteOpenDocumentCommand(object o)
        {
            var saveDialog = new Microsoft.Win32.OpenFileDialog();
            saveDialog.Filter = "TakoDeploy Document (*.tdd)|*.tdd|All files (*.*)|*.*";
            var result = saveDialog.ShowDialog();
            if (!result.HasValue || (result.HasValue && !result.Value)) return;
            if (!System.IO.File.Exists(saveDialog.FileName)) return;
            //using (var streaem = new MemoryStream())
            using (var stream = new System.IO.StreamReader(saveDialog.OpenFile()))
            {
                var data = Newtonsoft.Json.Linq.JObject.Parse(stream.ReadToEnd());
                var deployment = data.ToObject<Deployment>();
                if (deployment != null)
                {
                    DocumentManager.Open(deployment, saveDialog.SafeFileName);
                }
                else
                {
                    throw new Exception("potato");
                }
            }
        }

        private async void ExecuteSaveDocumentCommand(object o)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Filter = "TakoDeploy Document (*.tdd)|*.tdd|All files (*.*)|*.*";
            var result = saveDialog.ShowDialog();
            if (!result.HasValue || (result.HasValue && !result.Value)) return;
            var name = saveDialog.SafeFileName;
            if (!name.EndsWith(".tdd")) name += ".tdd";
            //using (var streaem = new MemoryStream())
            using (var stream = new System.IO.StreamWriter(saveDialog.OpenFile()))
            {
                var data = Newtonsoft.Json.Linq.JObject.FromObject(DocumentManager.Current.Deployment);
                stream.Write(data.ToString());
                DocumentManager.Current.CurrentFileName = saveDialog.SafeFileName;
            }
            //DocumentManager.Save();
        }

        private void ExecuteValidate(object o)
        {

        }

        private void ClosingEventHandler(object sender, MaterialDesignThemes.Wpf.DialogClosingEventArgs eventArgs)
        {
            //if ((bool)eventArgs.Parameter)
            //{
            //    var source = ((eventArgs.Session.Content as System.Windows.FrameworkElement)?.DataContext as SourceEditorViewModel)?.Source;
            //    DocumentManager.Current.Deployment.Sources.Add(source);
            //    DocumentManager.Current.Deployment.CallPropertyChanges();
            //}
        }

        private async void ExecuteRunDeployCommand(object o)
        {
            await DocumentManager.Current.Deploy();
        }

        private bool IsTreeItemSelected(object o)
        {
            return CanExecuteDocumentIsPresent(o) && TreeSelectedItem != null && !(TreeSelectedItem is TreeItemSourcesDataContext);
        }
        private bool CanExecuteDeployment(object o)
        {
            if (DocumentManager.Current == null) return false;
            if (!DocumentManager.Current.IsModified) return false;
            return true;
        }

        private bool CanExecuteDocumentIsPresent(object o)
        {
            if (DocumentManager.Current == null) return false;
            return true;
        }

    }

    public class TreeItemSourceDatabaseDataContext
    {
        public IEnumerable DataContext { get; set; }
        public string HeaderText { get; set; }
    }

    public abstract class TreeItemDataContext : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _headerText = "";
        public string HeaderText { get { return _headerText; } set { _headerText = value; OnPropertyChanged(); } }

    }

    public class TreeItemScriptDataContext : TreeItemDataContext
    {

        public TreeItemScriptDataContext()
        {
            HeaderText = "Scripts";
            DocumentManager.OnNewDocument += DocumentManager_OnNewDocument;
        }

        private void DocumentManager_OnNewDocument(object sender, EventArgs e)
        {
            DocumentManager.Current.Deployment.PropertyChanged += Deployment_PropertyChanged; ;
            DocumentManager.Current.Deployment.ScriptFiles.CollectionChanged += Scripts_CollectionChanged;
        }

        private void Deployment_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged("SubElements");
        }

        private void Scripts_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged("SubElements");
        }

        public IEnumerable<SqlScriptFile> SubElements
        {
            get
            {
                if (DocumentManager.Current == null) return new List<SqlScriptFile>();
                if (DocumentManager.Current.Deployment == null) return new List<SqlScriptFile>();
                if (DocumentManager.Current.Deployment.ScriptFiles == null) return new List<SqlScriptFile>();
                return DocumentManager.Current?.Deployment?.ScriptFiles;
            }
            set { }
        }
    }

    public class TreeItemSourcesDataContext : TreeItemDataContext
    {

        public TreeItemSourcesDataContext()
        {
            HeaderText = "Sources";
            DocumentManager.OnNewDocument += DocumentManager_OnNewDocument;
        }

        private void DocumentManager_OnNewDocument(object sender, EventArgs e)
        {
            DocumentManager.Current.Deployment.PropertyChanged += Deployment_PropertyChanged; ;
            DocumentManager.Current.Deployment.Sources.CollectionChanged += Sources_CollectionChanged;
        }

        private void Deployment_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged("SubElements");
        }

        private void Sources_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged("SubElements");
        }

        public IEnumerable<SourceDatabase> SubElements
        {
            get
            {
                if (DocumentManager.Current == null) return new List<SourceDatabase>();
                if (DocumentManager.Current.Deployment == null) return new List<SourceDatabase>();
                if (DocumentManager.Current.Deployment.Sources == null) return new List<SourceDatabase>();
                return DocumentManager.Current?.Deployment?.Sources;
            }
            set { }
        }
    }

    public class ButtonCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public ButtonCommand(Action<object> execute) : this(execute, null)
        {
            Console.WriteLine("ButtonCommand Ctor");
        }

        public ButtonCommand(Action<object> execute, Func<object, bool> canExecute)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));

            _execute = execute;
            _canExecute = canExecute ?? (x => true);
        }

        public bool CanExecute(object parameter)
        {
            ;
            return _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add
            {
                CommandManager.RequerySuggested += value;
            }
            remove
            {
                CommandManager.RequerySuggested -= value;
            }
        }

        public void Refresh()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    //public class TreeViewElement : INotifyPropertyChanged
    //{
    //    public event EventHandler<string> OnCommand;
    //    public event PropertyChangedEventHandler PropertyChanged;

    //    public string ImageLocation { get; set; }
    //    public string HeaderText { get { return  } }
    //    public string BackgroundColor { get; set; }
    //    public int Level { get; set; }
    //    public List<TreeViewElement> SubElements
    //    {
    //        get
    //        {
    //            var result = new List<TreeViewElement>();
    //            if (DataContext == null) return result;
    //            //if (!(DataContext is IEnumerable)) return result;

    //            foreach (var item in DataContext.DataContext as IEnumerable)
    //            {
    //                result.Add(new TreeViewElement() { });
    //            }
    //            return result;
    //        }
    //    }

    //    private MainViewModel.TreeItemDataContext _dataContext;
    //    public MainViewModel.TreeItemDataContext DataContext
    //    {
    //        get { return _dataContext; }
    //        set
    //        {
    //            if (_dataContext == value) return;
    //            if(_dataContext != null)
    //                _dataContext.PropertyChanged -= _dataContext_PropertyChanged;

    //            _dataContext = value;

    //            if (_dataContext != null)
    //                _dataContext.PropertyChanged += _dataContext_PropertyChanged;
    //        }
    //    }

    //    private void _dataContext_PropertyChanged(object sender, PropertyChangedEventArgs e)
    //    {
    //        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    //    }

    //    private ICommand _ItemCommand;

    //    public ICommand ItemCommand
    //    {
    //        get
    //        {
    //            if (_ItemCommand == null)
    //            {
    //                _ItemCommand = new RelayCommand((o) =>
    //                {
    //                    OnCommand?.Invoke(this, "");
    //                });
    //            }
    //            return _ItemCommand;
    //        }
    //    }

    //}

    public class RelayCommand : ICommand
    {
        #region Fields
        readonly Action<object> _execute;
        readonly Predicate<object> _canExecute;
        #endregion // Fields

        #region Constructors
        public RelayCommand(Action<object> execute)
            : this(execute, null)
        {
        }
        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");

            _execute = execute;
            _canExecute = canExecute;
        }
        #endregion // Constructors
        #region ICommand Members
        [DebuggerStepThrough]
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute(parameter);
        }
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        #endregion // ICommand Members
    }

    //[ValueConversion(typeof(object), typeof(ContextMenu))]
    //public class ItemToContextMenuConverter : IValueConverter
    //{
    //    public static ContextMenu FirstLevelContextMenu;
    //    public static ContextMenu SecondLevelContextMenu;

    //    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        var item = value as TreeViewElement;
    //        if (item == null) return null;

    //        return item.Level == 0 ? FirstLevelContextMenu : SecondLevelContextMenu;
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        throw new Exception("The method or operation is not implemented.");
    //    }
    //}
}
