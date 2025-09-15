using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MeetingMind;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button clickedButton)
        {
            // Reset all tab buttons to inactive state
            SummaryTab.Style = (Style)FindResource("TabButton");
            ActionItemsTab.Style = (Style)FindResource("TabButton");
            FollowUpsTab.Style = (Style)FindResource("TabButton");
            QuestionsTab.Style = (Style)FindResource("TabButton");
            RawSummaryTab.Style = (Style)FindResource("TabButton");

            // Hide all tab content
            SummaryContent.Visibility = Visibility.Collapsed;
            ActionItemsContent.Visibility = Visibility.Collapsed;
            FollowUpsContent.Visibility = Visibility.Collapsed;
            QuestionsContent.Visibility = Visibility.Collapsed;
            RawSummaryContent.Visibility = Visibility.Collapsed;

            // Set active tab button and show content
            switch (clickedButton.Name)
            {
                case "SummaryTab":
                    clickedButton.Style = (Style)FindResource("ActiveTabButton");
                    SummaryContent.Visibility = Visibility.Visible;
                    break;
                case "ActionItemsTab":
                    clickedButton.Style = (Style)FindResource("ActiveTabButton");
                    ActionItemsContent.Visibility = Visibility.Visible;
                    break;
                case "FollowUpsTab":
                    clickedButton.Style = (Style)FindResource("ActiveTabButton");
                    FollowUpsContent.Visibility = Visibility.Visible;
                    break;
                case "QuestionsTab":
                    clickedButton.Style = (Style)FindResource("ActiveTabButton");
                    QuestionsContent.Visibility = Visibility.Visible;
                    break;
                case "RawSummaryTab":
                    clickedButton.Style = (Style)FindResource("ActiveTabButton");
                    RawSummaryContent.Visibility = Visibility.Visible;
                    break;
            }
        }
    }
}