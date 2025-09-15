using MeetingMind.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MeetingMind.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void TabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                // Reset all tab buttons to inactive state
                SummaryTab.Background = System.Windows.Media.Brushes.LightGray;
                SummaryTab.BorderBrush = System.Windows.Media.Brushes.Gray;
                ActionItemsTab.Background = System.Windows.Media.Brushes.LightGray;
                ActionItemsTab.BorderBrush = System.Windows.Media.Brushes.Gray;
                FollowUpsTab.Background = System.Windows.Media.Brushes.LightGray;
                FollowUpsTab.BorderBrush = System.Windows.Media.Brushes.Gray;
                QuestionsTab.Background = System.Windows.Media.Brushes.LightGray;
                QuestionsTab.BorderBrush = System.Windows.Media.Brushes.Gray;
                RawSummaryTab.Background = System.Windows.Media.Brushes.LightGray;
                RawSummaryTab.BorderBrush = System.Windows.Media.Brushes.Gray;

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
                        clickedButton.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E3F2FD"));
                        clickedButton.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3"));
                        SummaryContent.Visibility = Visibility.Visible;
                        break;
                    case "ActionItemsTab":
                        clickedButton.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E3F2FD"));
                        clickedButton.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3"));
                        ActionItemsContent.Visibility = Visibility.Visible;
                        break;
                    case "FollowUpsTab":
                        clickedButton.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E3F2FD"));
                        clickedButton.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3"));
                        FollowUpsContent.Visibility = Visibility.Visible;
                        break;
                    case "QuestionsTab":
                        clickedButton.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E3F2FD"));
                        clickedButton.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3"));
                        QuestionsContent.Visibility = Visibility.Visible;
                        break;
                    case "RawSummaryTab":
                        clickedButton.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E3F2FD"));
                        clickedButton.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3"));
                        RawSummaryContent.Visibility = Visibility.Visible;
                        break;
                }
            }
        }
    }
}
