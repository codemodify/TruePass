using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text;
using System.Xml.Linq;

namespace TruePass
{
    public partial class MainWindow : Window
    {
        bool _paused;
        byte[] _randomBytes;
        byte[] _randomBytesAsAsciiEncoded;
        RNGCryptoServiceProvider _randomNumberGenerator;
        byte A, B, C;

        System.Windows.Forms.NotifyIcon _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();

            XDocument xDoc = LoadPasswordFile();

            InitTrayIcon();

            _paused = true;

            int bufferLength = 999;
            _randomBytes = new byte[ bufferLength ];
            _randomBytesAsAsciiEncoded = new byte[ _randomBytes.Length + bufferLength / 3 ];
            _randomNumberGenerator = new RNGCryptoServiceProvider();

            PauseButton_Click( null, null );
        }

        private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e )
        {
            e.Cancel = true;

            Hide();

            PauseButton_Click( null, null );
        }

        private void Window_Closed( object sender, EventArgs e )
        {
            _notifyIcon.Visible = false;
        }

        #region InitTrayIcon

        void InitTrayIcon()
        {
            BitmapImage bi = new BitmapImage( new Uri( "pack://application:,,,/TruePass;component/icon_24x24.png" ) );

            MemoryStream mse = new MemoryStream();
            PngBitmapEncoder 
                be = new PngBitmapEncoder();
                be.Frames.Add( BitmapFrame.Create( bi ) );
                be.Save( mse );

            System.Drawing.Bitmap b = new System.Drawing.Bitmap( mse );

            System.Windows.Forms.MenuItem
                mi = new System.Windows.Forms.MenuItem( "Exit" );
                mi.Click += delegate( object sender, EventArgs e )
                {
                    _paused = true;
                    Closing -= Window_Closing;

                    Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    Application.Current.Shutdown();
                };

            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = System.Drawing.Icon.FromHandle( b.GetHicon() );
            _notifyIcon.Visible = true;
            _notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu( new System.Windows.Forms.MenuItem[] { mi } );
            _notifyIcon.DoubleClick += new EventHandler( NotifyIcon_DoubleClick );
        }

        void NotifyIcon_DoubleClick( object sender, EventArgs e )
        {
            this.Show();

            PauseButton_Click( null, null );
        }

        #endregion

        #region ContinouslyGeneratePasswords

        void ContinouslyGeneratePasswords()
        {
            if( _paused )
                return;

            _randomNumberGenerator.GetBytes( _randomBytes );

            UUEncode( _randomBytes, ref _randomBytesAsAsciiEncoded );

            System.Text.ASCIIEncoding asciiEncoding = new System.Text.ASCIIEncoding();
            string generatedPassword1 = asciiEncoding.GetString( _randomBytesAsAsciiEncoded );

            string generatedPassword2 =  Convert.ToBase64String( _randomBytes );

            var sb = new StringBuilder();
            for( int i=0; i < generatedPassword1.Length; i++ )
            {
                sb.AppendFormat( "{0}{1}", generatedPassword1[i], generatedPassword2[i] );
            }

            string generatedPassword = sb.ToString();

            #region Async UI update

            //GeneratedPassword.Dispatcher.BeginInvoke
            //(
            //    DispatcherPriority.Normal,
            //    new 
            //    new DispatcherOperationCallback(delegate
            //    {
            //        GeneratedPassword.Text = generatedPassword;
            //        return null;
            //    }),
            //    null
            //);

            //object[] args = new object[] { generatedPassword };
            GeneratedPassword.Dispatcher.BeginInvoke( DispatcherPriority.Normal, (SetTextD) SetTextDelegate, generatedPassword );

            #endregion

            Thread.Sleep( 2000 );
            new Thread( ContinouslyGeneratePasswords ).Start();
        }

        private delegate void SetTextD( String a );
        private void SetTextDelegate( String generatedPassword )
        {
            GeneratedPassword.Text = generatedPassword as String;
        }

        private void PauseButton_Click( object sender, RoutedEventArgs e )
        {
            _paused = true;

            PauseButton.Visibility = Visibility.Hidden;
            ContinueButton.Visibility = Visibility.Visible;

            new Thread( ContinouslyGeneratePasswords ).Start();
        }

        private void ContinueButton_Click( object sender, RoutedEventArgs e )
        {
            _paused = false;

            PauseButton.Visibility = Visibility.Visible;
            ContinueButton.Visibility = Visibility.Hidden;

            new Thread( ContinouslyGeneratePasswords ).Start();
        }

        #endregion

        #region UUEncode

        static readonly byte[] UUEncMap = new byte[]
        {
          0x60, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27,
          0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F,
          0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37,
          0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F,
          0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47,
          0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F,
          0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57,
          0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F
        };

        public void UUEncode( byte[] i, ref byte[] o )
        {
            for( long iIndex=0, oIndex=0; iIndex < i.Length; iIndex += 3, oIndex+=4 )
            {
                //o[ index + 0 ] = UUEncMap[ ( i[ index + 0 ] >> 2 ) & 63 ];


                // 3-byte to 4-byte conversion + 0-63 to ascii printable conversion
                A = i[ iIndex + 0 ];
                B = i[ iIndex + 1 ];
                C = i[ iIndex + 2 ];

                o[ oIndex + 0 ] = UUEncMap[ ( A >> 2 ) & 63 ];
                o[ oIndex + 1 ] = UUEncMap[ ( B >> 4 ) & 15 | ( A << 4 ) & 63 ];
                o[ oIndex + 2 ] = UUEncMap[ ( C >> 6 ) & 3 | ( B << 2 ) & 63 ];
                o[ oIndex + 3 ] = UUEncMap[ C & 63 ];
            }
        }

        #endregion

        private void AddButton_Click( object sender, RoutedEventArgs e )
        {

        }

        private void FeedbackButton_Click( object sender, RoutedEventArgs e )
        {

        }

        private void CopyButton_Click( object sender, RoutedEventArgs e )
        {

        }

        private void EeditButton_Click( object sender, RoutedEventArgs e )
        {

        }

        private void GeneratedPassword_MouseMove( object sender, System.Windows.Input.MouseEventArgs e )
        {
            if( !String.IsNullOrEmpty( GeneratedPassword.SelectedText ) )
            {
                PauseButton_Click( null, null );
            }
        }

        #region Save / Load password file

        private XDocument LoadPasswordFile()
        {
            String appFullPath = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName );

            String passwordFile = String.Format
            (
                "{0}.truepass",
                Path.Combine( AppDomain.CurrentDomain.BaseDirectory, Path.GetFileNameWithoutExtension( appFullPath ) )
            );

            if( File.Exists( passwordFile ) )
            {
                return XDocument.Load( passwordFile );
            }

            return null;
        }

        #endregion
    }
}
