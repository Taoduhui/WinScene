using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WaveLib;
using WaveLib.AudioMixer;
using NAudio.Wave;
using MathNet;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Dsp;
using System.Windows.Forms;

namespace WpfApp11
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public IntPtr handle;
        public Keys hotkey = Keys.F3;
        static KBHook hook = null;
        static WaveInEvent recorder = new WaveInEvent();
        static WaveOut waveOut;
        static BufferedWaveProvider bufferedWaveProvider;
        Filter filter = new Filter();
        bool IsSpeaking = false;
        bool ServIsActive = false;
        double VolumeThreshold = 0;

        int cnt = 0;
        public MainWindow()
        {
            InitializeComponent();
            recorder.DataAvailable += Recorder_DataAvailable;
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                string wic = WaveIn.GetCapabilities(i).ProductName;
                ComboBoxItem item = new ComboBoxItem();
                item.Content = wic;
                SoundCardSelect.Items.Add(item);
            }

            SoundCardSelect.SelectionChanged += SoundCardSelect_SelectionChanged;
            SoundCardSelect.SelectedIndex = 0;
            recorder.WaveFormat = new WaveFormat(44000, 16, 1);
            recorder.StartRecording();
            NAudio.Mixer.MixerLine mixerLine = recorder.GetMixerLine();
            this.SourceInitialized += MainWindow_SourceInitialized;
        }

        ~MainWindow()
        {
            SetVolume(1);
        }

        private void Recorder_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (IsSpeaking||!ServIsActive)
            {
                return;
            }
            //double avrvolume = Math.Round(filter.AverVolume * 10000, 2);
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        private void SoundCardSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            recorder.DeviceNumber = SoundCardSelect.SelectedIndex;
        }

        private void MainWindow_SourceInitialized(object sender, System.EventArgs e)
        {
            handle = (new WindowInteropHelper(this)).Handle;
        }

        void SetVolume(float Volume)
        {
            Mixers mMixers = new Mixers();
            foreach (MixerDetail mixerDetail in mMixers.Recording.Devices)
            {
                int lineid = recorder.GetMixerLine().LineId;
                MixerLine mixerLine = mMixers.Recording.Lines.GetMixerLineByLineId((uint)lineid);
                mixerLine.Volume = Convert.ToInt32 ( Volume * mixerLine.VolumeMax);
            }
        }

        private void SetHotKeyBtn_Click(object sender, RoutedEventArgs e)
        {
            SetHotKeyBtn.Content = "等待按键";
            if (hook != null)
            {
                hook.SwitchToSetMode();
                return;
            }
            hook = new KBHook();
            hook.OnSetKey += Hook_OnSetKey;
        }

        private void Hook_OnSetKey()
        {
            SetHotKeyBtn.Content = hook.hotkey.ToString();
            hotkey = hook.hotkey;
            hook.SwitchToHotKeyMode();
        }

        private void Active_Click(object sender, RoutedEventArgs e)
        {
            if (!ServIsActive)
            {
                if (hook == null)
                {
                    hook = new KBHook(hotkey);
                }
                hook.OnHotKeyDown += Hook_OnHotKeyDown;
                hook.OnHotKeyUp += Hook_OnHotKeyUp;
                hook.Active = true;
                Active.Content = "停用";
                SetHotKeyBtn.IsEnabled = false;
                CutOffFreqSlider.IsEnabled = false;

                VolumeThreshold = VolumeThresholdSlider.Value;
                waveOut = new WaveOut();
                WaveFormat wf = new WaveFormat(44000, 16, 1);
                bufferedWaveProvider = new BufferedWaveProvider(wf);
                filter.setValues(bufferedWaveProvider.ToSampleProvider(), Convert.ToInt32(CutOffFreqSlider.Value));
                waveOut.Volume = 1;
                waveOut.Init(filter);
                waveOut.Play();
                ServIsActive = true;
            }
            else
            {
                SetHotKeyBtn.IsEnabled = true;
                CutOffFreqSlider.IsEnabled = true;
                ServIsActive = false;
                waveOut.Stop();
                hook.Active = false;
                Active.Content = "激活";
                GC.Collect();
            }

        }

        private void Hook_OnHotKeyUp()
        {
            SetVolume(0.1f);
            IsSpeaking = false;
        }

        private void Hook_OnHotKeyDown()
        {
            SetVolume(1);
            IsSpeaking = true;
        }

        private void VolumeThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            filter.Threshold = Convert.ToSingle(VolumeThresholdSlider.Value/1000);
        }

        private void VolumeAmpSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            filter.Amp = Convert.ToInt32(Math.Pow(10, VolumeAmpSlider.Value));
        }
    }

    public class Filter : ISampleProvider
    {
        private ISampleProvider sourceProvider;
        private BiQuadFilter[] filters;
        private int channels;
        private int cutOffFreq;
        public float AverVolume = 0;
        public float Threshold = 0.1f;
        int CountDown = 10;
        public int Amp=1;

        public void setValues(ISampleProvider sourceProvider, int cutOffFreq)
        {
            this.sourceProvider = sourceProvider;
            this.cutOffFreq = cutOffFreq;
            filter_LowPass();
        }

        private void filter_LowPass()
        {
            channels = sourceProvider.WaveFormat.Channels;
            filters = new BiQuadFilter[channels];

            for (int n = 0; n < channels; n++)
                if (filters[n] == null)
                    filters[n] = BiQuadFilter.LowPassFilter(44000, cutOffFreq, 1);
                else
                    filters[n].SetLowPassFilter(44000, cutOffFreq, 1);
        }

        public WaveFormat WaveFormat { get { return sourceProvider.WaveFormat; } }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);
            AverVolume = 0;
            for (int i = 0; i < samplesRead; i++)
            {
                float res= filters[(i % channels)].Transform(buffer[offset + i])*100;
                buffer[offset + i] = res * Amp;
                AverVolume +=Math.Abs (res);
            }
            AverVolume /= samplesRead;
            if(AverVolume< Threshold)
            {
                if (CountDown <= 0)
                {
                    for (int i = 0; i < samplesRead; i++)
                    {
                        float res = 0;
                        buffer[offset + i] = res;
                    }
                }
                else
                {
                    CountDown--;
                }
            }
            else
            {
                if (CountDown < 10)
                {
                    CountDown ++;
                }
            }
            return samplesRead;
        }
    }
}
