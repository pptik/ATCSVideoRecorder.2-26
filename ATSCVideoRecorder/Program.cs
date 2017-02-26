using AForge.Video.FFMPEG;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace ATSCVideoRecorder
{
    class Program
    {
        
        static void Main(string[] args)
        {
            var timer = new System.Threading.Timer(
              e => test(),
              null,
              TimeSpan.Zero,
              TimeSpan.FromMinutes(5));
            Console.WriteLine("Press enter to close...");
            Console.ReadLine();
        }

        private static void test()
        {

            try
            {
                int cctvcode = -1;
                string[] lines = System.IO.File.ReadAllLines(@"config.txt");
               
                foreach (string line in lines)
                {
                    string[] words = line.Split('|');
                    string directory = words[0];
                    string imagelink = words[1];
                    Console.WriteLine(directory);
                    Console.WriteLine(imagelink);

                    try
                    {
                        Thread thread = new Thread(() => getimagefromurl(cctvcode,imagelink, "ATCS/video/"+directory+"/",directory));
                        thread.Start();
                    }
                    catch (Exception)
                    {

                        Console.WriteLine("proccess failed please check connection");
                    }
                    cctvcode =cctvcode+ 1;

                }
            }
            catch (Exception)
            {

                Console.WriteLine("file not found");
            }
           

        }

        private static void getimagefromurl(int cctv_code, String url, String path, String tempname)
        {
            List<System.Drawing.Bitmap> ListBitmap = new List<System.Drawing.Bitmap>();
            string filename = tempname + DateTime.Now.ToString("mmssfff") + ".mp4";
            for (int i = 0; i < 250; i++)
            {
                try
                {
                    System.Net.WebRequest request = System.Net.WebRequest.Create(url);
                    request.Credentials = new NetworkCredential("admin", "12345");


                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        using (Stream responseStream = response.GetResponseStream())
                        {
                            Bitmap bitmap = new Bitmap(responseStream);
                            ListBitmap.Add(bitmap);
                        }
                    }
                }
                catch (Exception)
                {

                    //Console.WriteLine("fail get image "+tempname);
                }
                Thread.Sleep(1000);
            }
            int width = 352;
            int height = 288;
            var framRate = 25;

            try
            {
                using (var vFWriter = new VideoFileWriter())
                {
                    vFWriter.Open(filename, width, height, framRate, VideoCodec.MPEG4);


                    //loop throught all images in the collection
                    foreach (var bitmap in ListBitmap)
                    {
                        //what's the current image data?

                        var bmpReduced = ReduceBitmap(bitmap, width, height);

                        vFWriter.WriteVideoFrame(bmpReduced);
                    }
                    vFWriter.Close();
                }
            }
            catch (Exception)
            {

                Console.WriteLine("failed create video");
            }

            Console.WriteLine("Success Create Video "+filename);
            try
            {
                uploadtoftp(cctv_code,filename,path,tempname);
            }
            catch (Exception)
            {

                Console.WriteLine("failed upload to ftp " + filename);
            }
            try
            {
                string currentexedirectory = "C:/Users/Administrator/Desktop/atcsvideorecorder/";
                File.SetAttributes(currentexedirectory + filename, FileAttributes.Normal);
                File.Delete(currentexedirectory + filename);
                Console.WriteLine("success delete " + @filename);
            }
            catch (Exception)
            {
                Console.WriteLine("failed delete file " + @filename);
            }
            try
            {
                uploadtodb(cctv_code, path, filename);
                
                Console.WriteLine("success upload to DB " + @filename);
            }
            catch (Exception)
            {
                Console.WriteLine("failed upload to DB " + @filename);
            }


        }
        private static void uploadtodb(int cctv_code, string path, string name)
        {
            using (var conn = new MySqlConnection("xx"))
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand();
                cmd.Connection = conn;
                cmd.CommandText = "INSERT INTO atcs_video (name, path, cctv_code) VALUES('" + name + "', '" + path + "', " + cctv_code + ")";
                cmd.Prepare();
                cmd.ExecuteNonQuery();
             
            }
            Console.WriteLine("Success Upload Image to DB");
        }
        private static void uploadtoftp(int cctv_code, String filename, String path, String tempname)
        {
            
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create("xx:60328/" + path + filename);
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.UseBinary = true;
            request.UsePassive = true;
            request.KeepAlive = true;
            request.Credentials = new NetworkCredential("xx", "xx");


            using (FileStream fs = File.OpenRead(@filename))
            {
                byte[] buffer = new byte[fs.Length];
                fs.Read(buffer, 0, buffer.Length);
                fs.Close();
                Stream requestStream = request.GetRequestStream();
                requestStream.Write(buffer, 0, buffer.Length);
                requestStream.Flush();
                requestStream.Close();
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                Console.WriteLine("Upload File Complete, status {0}", response.StatusDescription);
                response.Close();
                Console.WriteLine("Success Upload Image to FTP");
            }

          


        }
        public static Bitmap ReduceBitmap(Bitmap original, int reducedWidth, int reducedHeight)
        {
            var reduced = new Bitmap(reducedWidth, reducedHeight);
            using (var dc = Graphics.FromImage(reduced))
            {
                // you might want to change properties like
                dc.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                dc.DrawImage(original, new Rectangle(0, 0, reducedWidth, reducedHeight), new Rectangle(0, 0, original.Width, original.Height), GraphicsUnit.Pixel);
            }

            return reduced;
        }

    }
}
