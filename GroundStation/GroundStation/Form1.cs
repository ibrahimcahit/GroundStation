﻿using System;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GroundStation
{
    public partial class Form1 : Form
    {
        public Simulation simulationObject;
        private VideoFeed videoFeed;
        public Mavlink mavlink;
        private DateTime FirstTelemetryTime;
        private bool didOperationBegin = false;
        public string[] data; // Newly
        public Form1()
        {
            
            this.FormClosed += Form1_FormClosed;
            InitializeComponent();
            videoFeed = new VideoFeed(camera_display, this);
            mavlink = new Mavlink(this,UpdateData,byteRecv,byteTrans,PermissionTimer,byteTotal,videoStatus);
            this.PermissionTimer.Tick += new System.EventHandler(mavlink.Permission_OVER_EVENT); // Added Manually After Check
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }

        public IntPtr getDrawSurface()
        {
            return simulation_display.Handle;
        }

        private void slct_file_Click(object sender, EventArgs e) // TODO: not working for now need to implement file selection
        {
            int size = -1;
            DialogResult result = openFileDialog1.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                string file = openFileDialog1.FileName;
                try
                {
                    string text = File.ReadAllText(file);
                    size = text.Length;
                    mavlink.SetVideoData(size,text);
                    labelFileName.Text = "Selected File: " + Path.GetFileName(file);
                }
                catch (IOException)
                {
                    MessageBox.Show("Can't open video file: "+ file);
                }
            }
            //Console.WriteLine(size); // <-- Shows file size in debugging mode.
            //Console.WriteLine(result); // <-- For debugging use.
        }

        private void button1_Click(object sender, EventArgs e)
        {
            mavlink.Connect_Event(sender,e);
            connectionLabel.BackColor = Color.Green;
            connectionLabel.Text = "Connected";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            gMap.MapProvider = GMap.NET.MapProviders.GoogleMapProvider.Instance;
            GMap.NET.GMaps.Instance.Mode = GMap.NET.AccessMode.ServerOnly;
            gMap.ShowCenter = false; // Disable the red cross on the map
            gMap.Position = new GMap.NET.PointLatLng(41.007848569582244, 28.98043706315273); // Ininitally point to Istanbul, since no data is present
            //gMap.Position = new GMap.NET.PointLatLng(45,45); // An example usage of lat long

        }

        public void updateLocation(float lat, float lng)
        {
            /*if(!(Math.Abs(lat) <= 90 && Math.Abs(lng) <= 180))
            {
                throw new Exception("Latitude data must be between -90 and 90. Longitude data must be between -180 and 180.");
            }*/ // I would use this to verify the data but instead I will let the gmap do it. Hopefully gmap implements errors better than me.

            gMap.Position = new GMap.NET.PointLatLng((double) lat,(double) lng);
            gMap.Zoom = 15;
            GMap.NET.WindowsForms.GMapOverlay markers = new GMap.NET.WindowsForms.GMapOverlay("markers");

            if (ConfigurationManager.AppSettings["enableGpsPin1"] == "true") { 
                GMap.NET.WindowsForms.GMapMarker marker1 =
            new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
            new GMap.NET.PointLatLng(lat, lng),
            new Bitmap(Resource1.cursor_image,new Size(72,55)));
                markers.Markers.Add(marker1);
            }
            if (ConfigurationManager.AppSettings["enableGpsPin2"] == "true") { 
            GMap.NET.WindowsForms.GMapMarker marker2 =
            new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
            new GMap.NET.PointLatLng(lat, lng),
            GMap.NET.WindowsForms.Markers.GMarkerGoogleType.blue_pushpin);
            markers.Markers.Add(marker2);
            }
            gMap.Overlays.Clear();
            gMap.Overlays.Add(markers);
            gMap.Update();
        }

        public void UpdateData(object sender, EventArgs e)
        {
            // 0->team_id, 1->pkg_number, 2-> gps time, 3->pressure, 4->alt, 5-> descend speed, 6->temp, 7-> BATTERY_VOLT,8->lat , 9->long , 10->alt 11->status, 12 ->pitch, 13->roll,14->yaw, 15->turn_number, 16->vid_info  // (subject to change)
            //string[] data = mavlink.Splitted_Telemetry;
            data = mavlink.Splitted_Telemetry;
            this.Invoke(new EventHandler(InsertDataRow)); // IMPORTANT NOTE: INSTEAD OF INVOKE, LET'S TRY TO USE BeginInvake (it is dangerous but can be used as a last resort)
            //InsertDataRow(data);

            // USE INCOMING DATA HERE
            if (!didOperationBegin) //Means that this is our first package, therefore initalize the begin time.
            {
                FirstTelemetryTime = DateTime.Now;
                didOperationBegin = true; // I don't like using flags but anyways :S
            }
            double operationTime = Math.Round((DateTime.Now - FirstTelemetryTime).TotalSeconds,2); // I did round to 2 decimal places. Idk how this would look.
            packGraph.Series[0].Points.AddXY(operationTime, Int32.Parse(data[1]));
            presGraph.Series[0].Points.AddXY(operationTime, Double.Parse(data[3]));
            altGraph.Series[0].Points.AddXY(operationTime, Double.Parse(data[4]));
            descSpeedGraph.Series[0].Points.AddXY(operationTime, Double.Parse(data[5]));
            tempGraph.Series[0].Points.AddXY(operationTime, Double.Parse(data[6]));
            voltageGraph.Series[0].Points.AddXY(operationTime, Double.Parse(data[7]));
            try
            {
                updateLocation(float.Parse(data[8]),float.Parse(data[9]));
            }catch (Exception)
            {
                Console.WriteLine("Error occured during updateLocation. Use this catch for details.");
            }
            

            statusLabel.Text = data[9];
            try
            {
                simulationObject.angleX = float.Parse(data[12]); 
                simulationObject.angleY = float.Parse(data[13]);
                simulationObject.angleZ = float.Parse(data[14]);
            }
            catch (Exception)
            {
                Console.WriteLine("Exception occured while setting using angle data for simulation"); //To be fair it is highly unlikely for an exception to occur here.
            }
            
        }

        private void InsertDataRow(object sender , EventArgs e ) // string[] data
        {
            // This restructing could be removed when the package is in order when got from mavlink.
            // I just filled unknown data with ??

            string gpsLat  = data[8];
            string gpsLong = data[9];
            string gpsAlt  = data[10];
            if (float.Parse(data[8]) == 0.00)
            {
                gpsLat = "NOT AVAILABLE";
            }
            if (float.Parse(data[9]) == 0.00)
            {
                gpsLong = "NOT AVAILABLE";
            }
            if (float.Parse(data[10]) == -1.00)
            {
                gpsAlt = "NOT AVAILABLE";
            }

            string[] NewRow = { data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7], gpsLat, gpsLong, gpsAlt, data[11], data[12], data[13], data[14], data[15], data[16] };
            dataGrid.Rows.Add(NewRow);
        }

        private void selectVidButton_Click(object sender, EventArgs e)
        {
            videoFeed.SelectFileSaveDir();
        }

        private void startVidButton_Click(object sender, EventArgs e)
        {
            videoFeed.startCapture();
        }

        private void stopVidButton_Click(object sender, EventArgs e)
        {
            videoFeed.stopCapture();
        }

        private void activeAlBut_Click(object sender, EventArgs e)
        {
            mavlink.Manual_Motor_Always_Click(sender, e);
        }

        private void active10SecBut_Click(object sender, EventArgs e)
        {
            mavlink.TenSecond_Active_Click(sender, e);
        }

        private void releaseBut_Click(object sender, EventArgs e)
        {
            mavlink.Manual_Release_Click(sender, e);
        }

        private void send_video_Click(object sender, EventArgs e)
        {
            mavlink.SEND_VIDEO_Click(sender, e);
        }

        private void exportBtn_Click(object sender, EventArgs e)
        {
            // creating Excel Application  
            Microsoft.Office.Interop.Excel._Application app = new Microsoft.Office.Interop.Excel.Application();
            // creating new WorkBook within Excel application  
            Microsoft.Office.Interop.Excel._Workbook workbook = app.Workbooks.Add(Type.Missing);
            // creating new Excelsheet in workbook  
            Microsoft.Office.Interop.Excel._Worksheet worksheet = null;
            // see the excel sheet behind the program  
            app.Visible = true;
            // get the reference of first sheet. By default its name is Sheet1.  
            // store its reference to worksheet  
            worksheet = workbook.Sheets["Sheet1"];
            worksheet = workbook.ActiveSheet;
            // changing the name of active sheet  
            worksheet.Name = "Exported Data";
            // storing header part in Excel  
            for (int i = 1; i < dataGrid.Columns.Count + 1; i++)
            {
                worksheet.Cells[1, i] = dataGrid.Columns[i - 1].HeaderText;
            }
            // storing Each row and column value to excel sheet  
            for (int i = 0; i < dataGrid.Rows.Count - 1; i++)
            {
                for (int j = 0; j < dataGrid.Columns.Count; j++)
                {
                    worksheet.Cells[i + 2, j + 1] = dataGrid.Rows[i].Cells[j].Value.ToString();
                }
            }
            // save the application  
            workbook.SaveAs(Path.Combine(Directory.GetCurrentDirectory(), "export-"+ DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".xls"), Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Microsoft.Office.Interop.Excel.XlSaveAsAccessMode.xlExclusive, Type.Missing, Type.Missing, Type.Missing, Type.Missing);
        }

        
    }
}
