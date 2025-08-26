using Microsoft.UI.Xaml.Controls;

namespace SHCNRSample
{
	public sealed partial class MainPage : Page
	{
		public MainPageViewModel ViewModel { get; } = new();

		public MainPage()
		{
			InitializeComponent();
		}

		private void AppBarToggleButton_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
		{

        }
    }
}
