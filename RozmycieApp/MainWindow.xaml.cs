using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
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
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.IO;
//using System.Windows.Forms;

namespace RozmycieApp
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public unsafe struct AsmParams
        {
            public byte* input;
            public byte* output;
            public Int64 radius;
            public float* kernell;
            public Int64 width;
            public Int64 height;
        }

        public float[] Kernell =
        {
            0.0675f, 0.125f, 0.0675f,
            0.125f, 0.25f, 0.125f,
            0.0675f, 0.125f, 0.0675f,
            //0.0670f, 0.12f, 0.0670f,
            //0.12f, 0.23f, 0.12f,
            //0.0670f, 0.12f, 0.0670f,
        };

        public const string CppFunctionDLL = @"RozmycieCPP.dll";

        // imports C++.dll
        [DllImport(CppFunctionDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int AddNumbers(int a, int b);  //funkcja sprawdzajaca polaczenie

        [DllImport(CppFunctionDLL, CallingConvention = CallingConvention.Cdecl)] //main part
        private static unsafe extern int SendToC(AsmParams* parameters);

        public const string asmFunctionsDLL = @"Asm.dll";
        // imports ASM .dll

        [DllImport(asmFunctionsDLL, CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern void gaussian_blur(AsmParams* parameters);

        public static AsmParams asmParameters = new AsmParams();

        private Bitmap bmp = null;
        private BitmapImage myBitmapImage = null;
        private Bitmap OutputBitmap = null;
        float[] r;
        float[] g;
        float[] b;
        float[] outputRed;
        float[] outputGreen;
        float[] outputBlue;

        // private byte[][] OutputByteArray;
        int radius = 1;
        int imageWidth = 0;
        int imageHeight = 0;
        int checkedOption = 0;
        float[,] kernel = new float[3, 3];

        public MainWindow()
        {
            InitializeComponent();
            bmp = null;
            myBitmapImage = null;
            OutputBitmap = null;
            RBtnAsm.IsChecked = true;
        }


        /**
          * Calculating Kernel
          **/
        public float[] CalculateKernel(int length, double weight)
        {
            double[,] Kernel = new double[length, length];
            float[] KernelF = new float[length * length];
            double sumTotal = 0;


            int kernelRadius = length / 2;
            double distance = 0;


            double calculatedEuler = 1.0 /
            (2.0 * Math.PI * Math.Pow(weight, 2));


            for (int filterY = -kernelRadius;
                 filterY <= kernelRadius; filterY++)
            {
                for (int filterX = -kernelRadius;
                    filterX <= kernelRadius; filterX++)
                {
                    distance = ((filterX * filterX) +
                               (filterY * filterY)) /
                               (2 * (weight * weight));


                    Kernel[filterY + kernelRadius,
                           filterX + kernelRadius] =
                           calculatedEuler * Math.Exp(-distance);


                    sumTotal += Kernel[filterY + kernelRadius,
                                       filterX + kernelRadius];
                }
            }


            for (int y = 0; y < length; y++)
            {
                for (int x = 0; x < length; x++)
                {
                    KernelF[y * length + x] = (float)(Kernel[y, x] *
                                   (1.0 / sumTotal));
                }
            }

            this.Kernell = KernelF;
            return KernelF;
        }


        /**
          * Loading metadata from bitmap -> in progress
          **/
        private void ReadingDataFromFile(Bitmap bmp)
        {
            byte[] data;
            using (var stream = new MemoryStream())
            {
                bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                data = stream.ToArray();
            }

            char b = Convert.ToChar(data[0x00]);
            char m = Convert.ToChar(data[0x01]);

            char[] contents = { 'B', 'M' };
            if (b == contents[0] && m == contents[1])
            {
                Console.WriteLine("File is Bitmap");
                LblSygnatura.Content = "BM";
            }
            else
            {
                Console.WriteLine("Format of the file is incorrect, it isn't a bitmap");
            }

            int size = data[0x04] + data[0x04 + 1] * 256 + data[0x04 + 2] * 65536 + data[0x04 + 3] * 16777216;
            // LblRozmiarPliku.Content = size;

            int offset = data[0x0a] + data[0x0a + 1] * 256 + data[0x0a + 2] * 65536 + data[0x0a + 3] * 16777216;
            if (offset != 54)
            {
                Console.WriteLine("Format nie obsługiwany: offset = " + offset);
            }
            else
            {
                LblOffset.Content = offset;
            }

            int width = data[0x12] + data[0x12 + 1] * 256 + data[0x12 + 2] * 65536 + data[0x12 + 3] * 16777216;
            int height = data[0x16] + data[0x16 + 1] * 256 + data[0x16 + 2] * 65536 + data[0x16 + 3] * 16777216;
            LblWymiary.Content = width + " x " + height;

            int bpp = data[0x1c] + data[0x1c + 1] * 256;
            if (bpp == 16 || bpp == 24 || bpp == 32)
            {
                LblKodowanie.Content = bpp;
            }
            else
            {
                Console.WriteLine("Format nie obsługiwany: " + bpp + " bit/pixel");
            }

            int kompresja = data[0x1e] + data[0x1e + 1] * 256 + data[0x1e + 2] * 65536 + data[0x1e + 3] * 16777216;
            if (kompresja == 0)
            {
                LblKompresja.Content = kompresja;
            }
            else
            {
                Console.WriteLine("Format nie obsługiwany");
            }
        }

            /**
             * Converts file to a bitmap.
             **/
            public void ConvertToBitmap(string fileName)
        {
            using (FileStream bmpStream = File.OpenRead(fileName))
            {
                System.Drawing.Image image = System.Drawing.Image.FromStream(bmpStream);

                bmp = new Bitmap(image);
            }
        }

        /**
        * Converts BitmapImage into an RGB arrays.
        **/
        private void BmpToArray(BitmapImage myBitmapImage)
        {
            imageWidth = bmp.Width * 3;// Red, Green, Blue values
            imageHeight = bmp.Height;
            int numberOfPixels = (imageHeight * imageWidth) / 3;
            r = new float[numberOfPixels];
            g = new float[numberOfPixels];
            b = new float[numberOfPixels];

            outputRed = new float[numberOfPixels];
            outputGreen = new float[numberOfPixels];
            outputBlue = new float[numberOfPixels];

            byte[] data;
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(myBitmapImage));
            using (MemoryStream ms = new MemoryStream())
            {
                encoder.Save(ms);
                data = ms.ToArray();
            }

            int counter = 0;

            for (int i = 0; i < imageHeight; ++i)
            {
                for (int j = 0; j < imageWidth / 3; ++j) // ArrayWidth / 3, each pixel in array takes 3 cells
                {
                    System.Drawing.Color pixel = bmp.GetPixel(j, i);

                    r[counter] = pixel.R;
                    g[counter] = pixel.G;
                    b[counter] = pixel.B;

                    counter++;
                }
            }
        }



        /**
         * Loads image and data after button Wgraj click.
         **/
        private void BtnWgraj_Click(object sender, RoutedEventArgs e)
        {

            OpenFileDialog fileBrowser = new OpenFileDialog();
            fileBrowser.Title = "Select a picture";
            fileBrowser.Filter = "All supported graphics|*.jpg;*.jpeg;*.png|" +
              "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
              "PNG (*.png)|*.png|" + "BMP (*.bmp)|*.bmp";
            if (fileBrowser.ShowDialog() == true)
            {
                ConvertToBitmap(fileBrowser.FileName); //converts file to a bitmap

                myBitmapImage = new BitmapImage();
                myBitmapImage.BeginInit();
                myBitmapImage.UriSource = new Uri(fileBrowser.FileName);
                myBitmapImage.EndInit();
                ImgUpload.Source = myBitmapImage; //sets the visual .source image

                ReadingDataFromFile(bmp); //reads metadata from bitmap 
                //BmpToArray(myBitmapImage);
            }
            
        }

        /**
         * Loads image and data after drop.
         **/
        private void ImgUpload_Drop(object sender, DragEventArgs e)
        {
            var data = e.Data.GetData(DataFormats.FileDrop);
            if (data != null)
            {
                var fileNames = data as string[];
                if (fileNames.Length > 0)
                {
                    ConvertToBitmap(fileNames[0]); //converts file to a bitmap
                    myBitmapImage = new BitmapImage();
                    myBitmapImage.BeginInit();
                    myBitmapImage.UriSource = new Uri(fileNames[0]);
                    myBitmapImage.EndInit();
                    ImgUpload.Source = myBitmapImage;
                    ReadingDataFromFile(bmp); //reads metadata from bitmap 
                                              //  BmpToArray(myBitmapImage);
                }
            }
        }

        /**
         * Loads image and data after mouse click.
         **/
        private void ImgUpload_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog fileBrowser = new OpenFileDialog();
            fileBrowser.Title = "Select a picture";
            fileBrowser.Filter = "All supported graphics|*.jpg;*.jpeg;*.png|" +
            "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
            "PNG (*.png)|*.png";
            if (fileBrowser.ShowDialog() == true)
            {
                ConvertToBitmap(fileBrowser.FileName); //converts file to a bitmap
                myBitmapImage = new BitmapImage();
                myBitmapImage.BeginInit();
                myBitmapImage.UriSource = new Uri(fileBrowser.FileName);
                myBitmapImage.EndInit();
                ImgUpload.Source = myBitmapImage;
                ReadingDataFromFile(bmp); //reads metadata from bitmap 
                                          // BmpToArray(myBitmapImage);
            }
        }

        /**
         * Slider value and connection to the displayed .Text in GUI 
         **/
        private void slValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Int32.TryParse(slValueNr.Text, out int RadiusValueFromTheTbx);
            radius = Int32.Parse(slValueNr.Text); // blur radius readed from gui
        }

        /**
         * Allows for drag and drop image.
         **/
        private void ImgUpload_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }

        /**
         * Saves filtered image in a computer --> Need to connect filtered image --> so in progress. 
         **/
        private void btnZapisz_Click(object sender, RoutedEventArgs e)
        {
            /// Create the file dialog
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Title = "Select a place to save the picture";
            saveFileDialog.Filter = "All supported graphics|*.jpg;*.jpeg;*.png|" +
              "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
              "PNG (*.png)|*.png";
            saveFileDialog.FilterIndex = 2;
            saveFileDialog.RestoreDirectory = true;

            if (OutputBitmap != null)
            {
                if (saveFileDialog.ShowDialog() == true)
                {
                    OutputBitmap.Save(saveFileDialog.FileName);
                }
            }
        }

        /**
         * Resets application settings -> in progress.
         **/
        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            ImgUpload.Source = new BitmapImage(new Uri("EmptyImageBackground.png", UriKind.Relative));
            bmp = null;
            slValue.Value = 0;
            TxBlCalculationTime.Text = "0";
            OutputBitmap = null;
            ImgOutput.Source = null;

        }

        private void RBtnC_Checked(object sender, RoutedEventArgs e)
        {
            RBtnC.IsChecked = true;

            checkedOption = 1;
        }

        private void RBtnAsm_Checked(object sender, RoutedEventArgs e)
        {
            RBtnAsm.IsChecked = true;
            checkedOption = 0;
        }

        private void BtnKonwertuj_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine(" Option: " + checkedOption);
            if (bmp == null)
            {
                MessageBox.Show("Choose input file!", "Conversion error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CalculateKernel(3, radius * 0.01);
            BmpToArray(myBitmapImage);

            unsafe
            {
                fixed (AsmParams* asmAdress = &asmParameters)
                {
                    fixed (float* kernell = Kernell)
                    {
                        var inputArray = new byte[imageWidth * imageHeight];
                        var outputArray = new byte[(imageWidth - 2) *(imageHeight - 2)];
                        var counter = 0;
                        for (var i = 0; i < imageWidth / 3 * imageHeight; i++)
                        {
                            inputArray[counter] = (byte) r[i];
                            ++counter;
                            inputArray[counter] = (byte) g[i];
                            ++counter;
                            inputArray[counter] = (byte) b[i];
                            ++counter;
                        }

                        fixed (byte* input = inputArray, output = outputArray)
                        {
                            asmParameters.input = input;
                            asmParameters.output = output;
                            asmParameters.radius = 1;
                            asmParameters.kernell = kernell;
                            asmParameters.width = imageWidth / 3;
                            asmParameters.height = imageHeight;

                            var watch = Stopwatch.StartNew();
                            if (checkedOption == 0)
                            {
                                gaussian_blur(asmAdress);
                            }
                            else
                            {
                                SendToC(asmAdress);
                            }
                            watch.Stop();
                            TxBlCalculationTime.Text = watch.ElapsedTicks.ToString();

                            counter = 0;

                            for (var i = 1; i < imageHeight - 1; i++)
                            {
                                for (var j = 1; j < imageWidth / 3 - 1; j++)
                                {
                                    outputRed[i * imageWidth / 3 + j] = output[counter];
                                    ++counter;
                                    outputGreen[i * imageWidth / 3 + j] = output[counter];
                                    ++counter;
                                    outputBlue[i * imageWidth / 3 + j] = output[counter];
                                    ++counter;
                                }
                            }
                        }

                        ArrayToBitmap();
                        ImgOutput.Source = BitmapToImageSource(OutputBitmap);
                    }
                }
            }
        }

        private void ArrayToBitmap()
        {
            OutputBitmap = new Bitmap(imageWidth / 3, imageHeight);

            int counter = 0;
            for (int i = 0; i < imageHeight; ++i)
            {
                for (int j = 0; j < imageWidth / 3; ++j)
                {
                    System.Drawing.Color color = System.Drawing.Color.FromArgb((int)outputRed[counter], (int)outputGreen[counter], (int)outputBlue[counter]);
                    OutputBitmap.SetPixel(j, i, color);
                    counter++;
                }
            }
        }

        private BitmapImage BitmapToImageSource(Bitmap bmp)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bmp.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;

                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

    }
}