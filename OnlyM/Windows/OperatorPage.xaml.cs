namespace OnlyM.Windows
{
    using System.Windows.Controls;
    using CommonServiceLocator;
    using Services.DragAndDrop;

    /// <summary>
    /// Interaction logic for OperatorPage.xaml
    /// </summary>
    public partial class OperatorPage : UserControl
    {
        public OperatorPage()
        {
            InitializeComponent();

            var dragAndDropService = ServiceLocator.Current.GetInstance<IDragAndDropService>();
            dragAndDropService.Init(this);
        }
    }
}
