using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using LOL_abilities_sound;

namespace LOL_abilities_sound
{
    public class MicrophoneItem
    {
        #region Properties
        public int Index { get; set; }
        public string Name { get; set; }
        #endregion

        #region Methods
        public override string ToString()
        {
            return Name ?? $"Device {Index}";
        }
        #endregion
    }

    public class VoiceProcessor : IDisposable
    {
        #region Constants
        private const int SAMPLE_RATE = 48000;
        private const int CHANNELS = 2;
        private const int LATENCY_MS = 20;
        private const float DEFAULT_MICROPHONE_VOLUME = 0.7f;
        private const string VB_CABLE_NAME = "VB-Audio Virtual Cable";
        private const string VB_CABLE_INPUT_SUFFIX = "Input";
        #endregion

        #region Fields
        private WasapiCapture _microphoneCapture;
        private WasapiOut _outputToVbCable;
        private WasapiOut _localAudioOutput;
        private MixingSampleProvider _vbCableMixer;
        private MixingSampleProvider _localAudioMixer;
        private WaveFormat _waveFormat;
        private bool _isProcessing;
        private bool _denoiseEnabled = false;
        private BufferedWaveProvider _microphoneBuffer;
        private VolumeSampleProvider _microphoneVolume;
        private MMDevice _defaultOutputDevice;
        private bool _disposed = false;
        #endregion

        #region Properties
        public bool DenoiseEnabled
        {
            get => _denoiseEnabled;
            set => _denoiseEnabled = value;
        }

        public bool IsProcessing => _isProcessing;
        #endregion

        #region Constructor
        public VoiceProcessor(int deviceNumber = 0)
        {
            try
            {
                Initialize(deviceNumber);
                System.Diagnostics.Debug.WriteLine("VoiceProcessor initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing VoiceProcessor: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region Static Methods
        public static List<MicrophoneItem> GetAvailableMicrophones()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting microphone discovery...");
                
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                var deviceList = new List<MicrophoneItem>();
                
                int index = 0;
                foreach (var device in devices)
                {
                    try
                    {
                        string deviceName = device.FriendlyName ?? $"Unknown Device {index}";
                        deviceList.Add(new MicrophoneItem { Index = index, Name = deviceName });
                        System.Diagnostics.Debug.WriteLine($"Found device {index}: {deviceName}");
                        index++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing device {index}: {ex.Message}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Total devices found: {deviceList.Count}");
                
                if (deviceList.Count == 0)
                {
                    var fallbackDevice = new MicrophoneItem { Index = 0, Name = "No microphones found" };
                    deviceList.Add(fallbackDevice);
                    System.Diagnostics.Debug.WriteLine("No microphones found, added fallback device");
                }
                
                return deviceList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting microphone list: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                return new List<MicrophoneItem> 
                { 
                    new MicrophoneItem { Index = 0, Name = $"Error: {ex.Message}" } 
                };
            }
        }
        #endregion

        #region Public Methods
        public void ChangeMicrophone(int deviceNumber)
        {
            try
            {
                bool wasRunning = _isProcessing;
                
                if (wasRunning)
                {
                    Stop();
                }

                DisposeResources();
                Initialize(deviceNumber);

                if (wasRunning)
                {
                    Start();
                }
                
                System.Diagnostics.Debug.WriteLine($"Successfully changed to microphone {deviceNumber}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error changing microphone: {ex.Message}");
                throw;
            }
        }

        public void Start()
        {
            try
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(VoiceProcessor));
                }

                _isProcessing = true;
                _outputToVbCable?.Play();
                _microphoneCapture?.StartRecording();
                _localAudioOutput?.Play();
                
                System.Diagnostics.Debug.WriteLine("VoiceProcessor started successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting VoiceProcessor: {ex.Message}");
                throw;
            }
        }

        public void Stop()
        {
            try
            {
                _isProcessing = false;
                _microphoneCapture?.StopRecording();
                _outputToVbCable?.Stop();
                _localAudioOutput?.Stop();
                
                System.Diagnostics.Debug.WriteLine("VoiceProcessor stopped successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping VoiceProcessor: {ex.Message}");
            }
        }

        public void PlaySound(string filePath, float volume = 1.0f)
        {
            try
            {
                if (_disposed)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot play sound: VoiceProcessor is disposed");
                    return;
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    System.Diagnostics.Debug.WriteLine("Cannot play sound: file path is null or empty");
                    return;
                }

                PlaySoundToVbCable(filePath, volume);
                PlaySoundLocally(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing sound '{filePath}': {ex.Message}");
            }
        }
        #endregion

        #region Private Methods
        private void Initialize(int deviceNumber)
        {
            try
            {
                SetupAudioFormat();
                SetupVbCableOutput();
                SetupMicrophoneCapture(deviceNumber);
                SetupLocalAudioOutput();
                SetupMixers();
                
                System.Diagnostics.Debug.WriteLine($"VoiceProcessor initialized with device {deviceNumber}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during initialization: {ex.Message}");
                DisposeResources();
                throw;
            }
        }

        private void SetupAudioFormat()
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SAMPLE_RATE, CHANNELS);
        }

        private void SetupVbCableOutput()
        {
            var enumerator = new MMDeviceEnumerator();
            var vbCable = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .FirstOrDefault(d => d.FriendlyName.Contains(VB_CABLE_NAME) && d.FriendlyName.Contains(VB_CABLE_INPUT_SUFFIX));
            
            if (vbCable == null)
            {
                throw new InvalidOperationException("VB-Cable not found! Please install VB-Audio Virtual Cable.");
            }

            _outputToVbCable = new WasapiOut(vbCable, AudioClientShareMode.Shared, false, LATENCY_MS);
        }

        private void SetupMicrophoneCapture(int deviceNumber)
        {
            var enumerator = new MMDeviceEnumerator();
            var microphoneDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
            
            if (deviceNumber >= microphoneDevices.Count)
            {
                throw new ArgumentException($"Microphone with index {deviceNumber} not found!");
            }
            
            var microphoneDevice = microphoneDevices[deviceNumber];
            _microphoneCapture = new WasapiCapture(microphoneDevice) { WaveFormat = _waveFormat };
            
            SetupMicrophoneBuffer();
        }

        private void SetupMicrophoneBuffer()
        {
            _microphoneBuffer = new BufferedWaveProvider(_waveFormat);
            _microphoneCapture.DataAvailable += (s, e) =>
            {
                try
                {
                    _microphoneBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error adding microphone samples: {ex.Message}");
                }
            };
            
            var microphoneSampleProvider = _microphoneBuffer.ToSampleProvider();
            _microphoneVolume = new VolumeSampleProvider(microphoneSampleProvider) 
            { 
                Volume = DEFAULT_MICROPHONE_VOLUME 
            };
        }

        private void SetupLocalAudioOutput()
        {
            var enumerator = new MMDeviceEnumerator();
            _defaultOutputDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _localAudioOutput = new WasapiOut(_defaultOutputDevice, AudioClientShareMode.Shared, false, LATENCY_MS);
        }

        private void SetupMixers()
        {
            _vbCableMixer = new MixingSampleProvider(_waveFormat) { ReadFully = true };
            _vbCableMixer.AddMixerInput(_microphoneVolume);
            _outputToVbCable.Init(_vbCableMixer);

            _localAudioMixer = new MixingSampleProvider(_waveFormat) { ReadFully = true };
            _localAudioOutput.Init(_localAudioMixer);
        }

        private void PlaySoundToVbCable(string filePath, float volume)
        {
            try
            {
                var reader = new AudioFileReader(filePath);
                var provider = ConvertToTargetFormat(reader);
                var volumeProvider = new VolumeSampleProvider(provider) { Volume = volume };
                var oneShot = new OffsetSampleProvider(volumeProvider) { Take = reader.TotalTime };
                
                _vbCableMixer?.AddMixerInput(oneShot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing sound to VB-Cable: {ex.Message}");
            }
        }

        private void PlaySoundLocally(string filePath)
        {
            try
            {
                var reader = new AudioFileReader(filePath);
                var provider = ConvertToTargetFormat(reader);
                var volumeProvider = new VolumeSampleProvider(provider) { Volume = Config.getSelfSoundVolume() };
                var oneShot = new OffsetSampleProvider(volumeProvider) { Take = reader.TotalTime };
                
                _localAudioMixer?.AddMixerInput(oneShot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing sound locally: {ex.Message}");
            }
        }

        private ISampleProvider ConvertToTargetFormat(ISampleProvider provider)
        {
            // Convert to stereo if needed
            if (provider.WaveFormat.Channels != _waveFormat.Channels)
            {
                provider = new MonoToStereoSampleProvider(provider);
            }
            
            // Convert sample rate if needed
            if (provider.WaveFormat.SampleRate != _waveFormat.SampleRate)
            {
                provider = new WdlResamplingSampleProvider(provider, _waveFormat.SampleRate);
            }
            
            // Convert to float if needed
            if (provider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                provider = new SampleToWaveProvider(provider).ToSampleProvider();
            }
            
            return provider;
        }

        private void DisposeResources()
        {
            try
            {
                _microphoneCapture?.Dispose();
                _microphoneCapture = null;
                
                _outputToVbCable?.Dispose();
                _outputToVbCable = null;
                
                _localAudioOutput?.Dispose();
                _localAudioOutput = null;
                
                _microphoneBuffer = null;
                _microphoneVolume = null;
                _vbCableMixer = null;
                _localAudioMixer = null;
                _defaultOutputDevice = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing resources: {ex.Message}");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stop();
                    DisposeResources();
                }
                
                _disposed = true;
            }
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~VoiceProcessor()
        {
            Dispose(false);
        }
        #endregion
    }
}
