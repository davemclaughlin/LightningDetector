using System;
using Microsoft.SPOT;
using Modules.EmbeddedAdventures;
using System.Threading;
using System.IO;
using System.Text;
using Microsoft.SPOT.IO;
using Microsoft.SPOT.Hardware;
using MFToolkit.Net.XBee;
using MFToolkit.IO;

namespace LightningDetector
{
    public class Program
    {
        static LightningDetectorI2C lightning;
        static XBee xbee;
        static int distance;
        static int energy;
        static bool noise = false;
        static bool disturance = false;
        static bool lightningDetected = false;
        static long msTimer;
        static InputPort locationSetting;
        //
        // Xbee stuff
        //
        static String XbeeMessage;
        static String[] XbeeStripped;
        static ZNetRxResponse who;
        static AtCommandResponse responce;

        public static void Main()
        {
            String msg;

            locationSetting = new InputPort(GHI.Pins.FEZCerbuinoBee.Headers.Gpio.A0, false, Port.ResistorMode.PullUp);

            xbee = new XBee("COM1", 9600, ApiType.Enabled);
            xbee.FrameReceived += xbee_FrameReceived;
            xbee.Open();

            lightning = new LightningDetectorI2C(4);

            lightning.LightningDetected += lightning_LightningDetected;
            lightning.DisturbanceDetected += lightning_DisturbanceDetected;
            lightning.NoiseDetected += lightning_NoiseDetected;
            if (locationSetting.Read() == false)
            {
                lightning.AFEGainBoost = LightningDetectorI2C.AFESetting.Outdoor;
            }
            msTimer = (DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond) + 5000;    // Send heartbeat every 5 seconds

            while(true)
            {
                if (lightningDetected)
                {
                    lightningDetected = false;

                    msg = "LIGHTNING," + distance.ToString() + "," + energy.ToString();

                    sendXbee(msg, 0x13A200409ED746);
                }
                else
                {
                    if((DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond) > msTimer)
                    {
                        msTimer = (DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond) + 5000;    // Send heartbeat every 5 seconds

                        msg = "LIGHTNING,HEARTBEAT";

                        sendXbee(msg, 0x13A200409ED746);
                    }
                }
                Thread.Sleep(200);
            }
        }

        static void xbee_FrameReceived(object sender, FrameReceivedEventArgs e)
        {
            if (e.Response.ApiID == XBeeApiType.ZNetRxPacket)
            {
                try
                {
                    who = e.Response as ZNetRxResponse;
                    ulong from = who.SerialNumber.Value;

                    XbeeMessage = new String(System.Text.Encoding.UTF8.GetChars(who.Value));

                    XbeeStripped = XbeeMessage.Split(',');

                    if (XbeeStripped[0].Equals("LIGHTNING"))        // Lightning?
                    {
                        if (XbeeStripped.Length >= 2)               // Data?
                        {
                            if(XbeeStripped[1] == "SETCOUNT5")
                            {
                                lightning.MinimumLightning = LightningDetectorI2C.MinNumberLightning.Five;
                            }
                            else if (XbeeStripped[1] == "SETCOUNT1")
                            {
                                lightning.MinimumLightning = LightningDetectorI2C.MinNumberLightning.One;
                            }
                        }
                    }
                }
                catch
                {
                    Debug.Print("XBEE message decode error");
                }
            }
        }

        static void lightning_DisturbanceDetected(LightningDetectorI2C sender)
        {
            disturance = true;    
        }

        static void lightning_NoiseDetected(LightningDetectorI2C sender)
        {
            noise = true;
        }

        static void lightning_LightningDetected(LightningDetectorI2C sender, LightningDetectorI2C.LightningEventArgs e)
        {
            distance = e.Distance;
            energy = e.Energy;

            lightningDetected = true;
        }

        static void sendXbee(String packet, ulong address)
        {
            XBeeResponse reply;

            XBeeRequest req = new ZNetTxRequest(new XBeeAddress64(address), new XBeeAddress16(0xFFFE), Encoding.UTF8.GetBytes(packet));
            try
            {
                reply = xbee.Execute(req, 2000);
            }
            catch (Exception)
            {
            }

        }
    }
}
