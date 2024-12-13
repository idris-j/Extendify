using System;
using System.IO;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using SharpAvi;
using SharpAvi.Output;
using SharpAvi.Codecs;

namespace Extendify
{
    public class ScreenRecorder
    {
        private bool isRecording = false;
        private bool isPaused = false;
        private bool isMuted = false;
        private string outputPath;
        
        private WasapiLoopbackCapture systemAudioCapture;
        private WaveInEvent microphoneCapture;
        private AviWriter aviWriter;
        private IAviVideoStream videoStream;
        private IAviAudioStream audioStream;
        private Timer frameTimer;
        private Bitmap screenBitmap;
        private Rectangle screenBounds;

        public void StartRecording(string path, int screenWidth, int screenHeight)
        {
            this.outputPath = path;
            string outputFile = GenerateOutputFilename();
            screenBounds = new Rectangle(0, 0, screenWidth, screenHeight);
            screenBitmap = new Bitmap(screenWidth, screenHeight);

            aviWriter = new AviWriter(outputFile)
            {
                FramesPerSecond = 20,
                EmitIndex1 = true
            };

            videoStream = aviWriter.AddVideoStream();
            videoStream.Width = screenWidth;
            videoStream.Height = screenHeight;
            videoStream.Codec = CodecIds.Uncompressed;

            audioStream = aviWriter.AddAudioStream();
            var waveFormat = new WaveFormat(44100, 16, 2);
            audioStream.SamplesPerSecond = waveFormat.SampleRate;
            audioStream.BitsPerSample = (short)waveFormat.BitsPerSample;

            frameTimer = new Timer();
            frameTimer.Interval = 33;
            frameTimer.Tick += CaptureFrame;
            frameTimer.Start();

            InitializeAudioCapture();
            isRecording = true;
        }

        private string GenerateOutputFilename()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(outputPath, $"Recording_{timestamp}.avi");
        }

        private void InitializeAudioCapture()
        {
            systemAudioCapture = new WasapiLoopbackCapture();
            systemAudioCapture.DataAvailable += (s, e) =>
            {
                if (!isPaused && !isMuted)
                {
                    audioStream.WriteBlock(e.Buffer, 0, e.BytesRecorded);
                }
            };
            systemAudioCapture.StartRecording();

            microphoneCapture = new WaveInEvent();
            microphoneCapture.WaveFormat = new WaveFormat(44100, 16, 1);
            microphoneCapture.DataAvailable += (s, e) =>
            {
                if (!isPaused && !isMuted)
                {
                    audioStream.WriteBlock(e.Buffer, 0, e.BytesRecorded);
                }
            };
            microphoneCapture.StartRecording();
        }

        private void CaptureFrame(object sender, EventArgs e)
        {
            if (!isRecording || isPaused) return;

            using (Graphics g = Graphics.FromImage(screenBitmap))
            {
                g.CopyFromScreen(screenBounds.Location, Point.Empty, screenBounds.Size);
            }

            using (var bitmap = new Bitmap(screenBitmap))
            {
                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppRgb);

                byte[] frameData = new byte[bitmapData.Stride * bitmapData.Height];
                System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, frameData, 0, frameData.Length);
                videoStream.WriteFrame(true, frameData, 0, frameData.Length);
                bitmap.UnlockBits(bitmapData);
            }
        }

        public void StopRecording()
        {
            isRecording = false;
            frameTimer?.Stop();
            systemAudioCapture?.StopRecording();
            microphoneCapture?.StopRecording();
            
            aviWriter?.Close();
            screenBitmap?.Dispose();
            CleanupResources();
        }

        public void PauseRecording()
        {
            isPaused = true;
            systemAudioCapture?.StopRecording();
            microphoneCapture?.StopRecording();
        }

        public void ResumeRecording()
        {
            isPaused = false;
            systemAudioCapture?.StartRecording();
            microphoneCapture?.StartRecording();
        }

        public void ToggleMicrophone(bool muted)
        {
            if (microphoneCapture == null) return;
            
            if (muted)
            {
                microphoneCapture.StopRecording();
            }
            else if (isRecording && !isPaused)
            {
                microphoneCapture.StartRecording();
            }
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
            if (systemAudioCapture == null) return;

            if (isMuted)
            {
                systemAudioCapture.StopRecording();
            }
            else if (isRecording && !isPaused)
            {
                systemAudioCapture.StartRecording();
            }
        }

        private void CleanupResources()
        {
            frameTimer?.Dispose();
            systemAudioCapture?.Dispose();
            microphoneCapture?.Dispose();
            screenBitmap?.Dispose();
        }
    }
}