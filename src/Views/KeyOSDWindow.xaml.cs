using System.Windows;

namespace EasyScreenRecord.Views
{
    public partial class KeyOSDWindow : Window
    {
        public KeyOSDWindow()
        {
            InitializeComponent();
            
            // Initial positioning will be handled by Service
        }

        public void SetText(string text)
        {
            if (KeyText.Text != text)
            {
                KeyText.Text = text;
            }
        }
    }
}
