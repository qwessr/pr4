using System.Collections.Generic;
using System.Windows;

namespace WpfClient
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow(List<string> history)
        {
            InitializeComponent();
            ListHistory.ItemsSource = history;
        }

    }
}
