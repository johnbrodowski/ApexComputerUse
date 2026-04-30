using System.Drawing;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUICapture = FlaUI.Core.Capturing.Capture;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using SysImaging = System.Drawing.Imaging;

namespace ApexComputerUse
{
    /// <summary>
    /// Reusable multimodal LLM helper using LLamaSharp's MTMD (multimodal) API.
    /// Every request is fully stateless \- no chat history is retained between calls.
    ///
    /// Usage:
    ///   var helper = new MtmdHelper(modelPath, mmProjPath);
    ///   await helper.InitializeAsync();
    ///   string result = await helper.DescribeImageAsync(@"C:\photo.jpg", "What is in this image?");
    /// </summary>
    public class MtmdHelper : IDisposable
    {
        private readonly string        _modelPath;
        private readonly string        _mmProjPath;
        private readonly MtmdOptions   _options;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private LLamaWeights? _model;
        private LLamaContext? _context;
        private MtmdWeights?  _clipModel;
        private string        _mediaMarker = "<media>";

        public bool IsInitialized  { get; private set; }
        public bool SupportsVision { get; private set; }
        public bool SupportsAudio  { get; private set; }

        // \-\- Construction \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        public MtmdHelper(string modelPath, string mmProjPath, MtmdOptions? options = null)
        {
            _modelPath  = modelPath;
            _mmProjPath = mmProjPath;
            _options    = options ?? new MtmdOptions();
        }

        // \-\- Initialization \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        /// <summary>Loads the model and multimodal projector into memory.</summary>
        public async Task InitializeAsync()
        {
            var modelParams = new ModelParams(_modelPath);

            var mtmdParams = MtmdContextParams.Default();
            mtmdParams.UseGpu = _options.UseGpu;

            _model     = await LLamaWeights.LoadFromFileAsync(modelParams);
            _context   = _model.CreateContext(modelParams);
            _clipModel = await MtmdWeights.LoadFromFileAsync(_mmProjPath, _model, mtmdParams);

            _mediaMarker   = mtmdParams.MediaMarker ?? NativeApi.MtmdDefaultMarker() ?? "<media>";
            SupportsVision = _clipModel.SupportsVision;
            SupportsAudio  = _clipModel.SupportsAudio;

            IsInitialized = true;
        }

        // \-\- Image \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        /// <summary>Loads an image from disk and asks the model about it.</summary>
        public async Task<string> DescribeImageAsync(
            string imagePath,
            string prompt = "Describe this image.")
        {
            EnsureInitialized();
            EnsureVision();

            _clipModel!.ClearMedia();
            var embed = _clipModel!.LoadMedia(imagePath);
            return await RunInferenceAsync($"{_mediaMarker} {prompt}", [embed]);
        }

        /// <summary>Encodes raw image bytes and asks the model about them.</summary>
        public async Task<string> DescribeImageAsync(
            byte[] imageBytes,
            string prompt = "Describe this image.")
        {
            EnsureInitialized();
            EnsureVision();

            _clipModel!.ClearMedia();
            var embed = _clipModel!.LoadMedia(new ReadOnlySpan<byte>(imageBytes));
            return await RunInferenceAsync($"{_mediaMarker} {prompt}", [embed]);
        }

        /// <summary>Loads multiple images from disk and asks the model about all of them.</summary>
        public async Task<string> DescribeImagesAsync(
            IEnumerable<string> imagePaths,
            string prompt = "Describe these images.")
        {
            EnsureInitialized();
            EnsureVision();

            _clipModel!.ClearMedia();
            var embeds  = new List<SafeMtmdEmbed>();
            var markers = new StringBuilder();

            foreach (var path in imagePaths)
            {
                embeds.Add(_clipModel!.LoadMedia(path));
                markers.Append(_mediaMarker).Append(' ');
            }

            return await RunInferenceAsync($"{markers}{prompt}", embeds);
        }

        // \-\- Audio \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        /// <summary>Loads an audio file from disk and asks the model about it.</summary>
        public async Task<string> DescribeAudioAsync(
            string audioPath,
            string prompt = "Transcribe or describe this audio.")
        {
            EnsureInitialized();
            EnsureAudio();

            _clipModel!.ClearMedia();
            var embed = _clipModel!.LoadMedia(audioPath);
            return await RunInferenceAsync($"{_mediaMarker} {prompt}", [embed]);
        }

        /// <summary>Encodes raw audio bytes and asks the model about them.</summary>
        public async Task<string> DescribeAudioAsync(
            byte[] audioBytes,
            string prompt = "Transcribe or describe this audio.")
        {
            EnsureInitialized();
            EnsureAudio();

            _clipModel!.ClearMedia();
            var embed = _clipModel!.LoadMedia(new ReadOnlySpan<byte>(audioBytes));
            return await RunInferenceAsync($"{_mediaMarker} {prompt}", [embed]);
        }

        // \-\- Video \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        /// <summary>
        /// Video input is not currently supported by MTMD.
        /// This method is a placeholder for future support.
        /// </summary>
        public Task<string> DescribeVideoAsync(string videoPath, string prompt = "")
            => throw new NotSupportedException("Video inputs are not supported by MTMD.");

        /// <summary>
        /// Video input is not currently supported by MTMD.
        /// This method is a placeholder for future support.
        /// </summary>
        public Task<string> DescribeVideoAsync(byte[] videoBytes, string prompt = "")
            => throw new NotSupportedException("Video inputs are not supported by MTMD.");

        // \-\- FlaUI element capture \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        /// <summary>Captures a UI element via FlaUI and asks the model to describe it.</summary>
        public async Task<string> DescribeElementAsync(
            AutomationElement element,
            string prompt = "Describe what you see in this UI element.")
        {
            EnsureInitialized();
            EnsureVision();

            _clipModel!.ClearMedia();
            var embed = _clipModel!.LoadMedia(new ReadOnlySpan<byte>(CaptureElementToPng(element)));
            return await RunInferenceAsync($"{_mediaMarker} {prompt}", [embed]);
        }

        /// <summary>Captures multiple UI elements via FlaUI and asks the model to describe them.</summary>
        public async Task<string> DescribeElementsAsync(
            IEnumerable<AutomationElement> elements,
            string prompt = "Describe what you see in these UI elements.")
        {
            EnsureInitialized();
            EnsureVision();

            _clipModel!.ClearMedia();
            var embeds  = new List<SafeMtmdEmbed>();
            var markers = new StringBuilder();

            foreach (var element in elements)
            {
                embeds.Add(_clipModel!.LoadMedia(new ReadOnlySpan<byte>(CaptureElementToPng(element))));
                markers.Append(_mediaMarker).Append(' ');
            }

            return await RunInferenceAsync($"{markers}{prompt}", embeds);
        }

        // \-\- Core inference \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        private async Task<string> RunInferenceAsync(string userPrompt, List<SafeMtmdEmbed> embeds)
        {
            await _gate.WaitAsync();
            try
            {
                // Clear KV cache so every request starts completely fresh
                _context!.NativeHandle.MemoryClear();

                // Fresh executor per request \- no state carried over
                var executor = new InteractiveExecutor(_context, _clipModel!);
                foreach (var e in embeds) executor.Embeds.Add(e);

                // Single-turn prompt \- no history
                var singleTurn = new ChatHistory();
                singleTurn.AddMessage(AuthorRole.User, userPrompt);
                var formattedPrompt = FormatChatHistory(_model!, singleTurn, addAssistant: true);

                var inferenceParams = new InferenceParams
                {
                    SamplingPipeline = new DefaultSamplingPipeline
                    {
                        Temperature = _options.Temperature
                    },
                    AntiPrompts = new List<string> { "User:" },
                    MaxTokens   = _options.MaxTokens
                };

                var sb = new StringBuilder();
                await foreach (var text in executor.InferAsync(formattedPrompt, inferenceParams))
                    sb.Append(text);

                foreach (var e in executor.Embeds) e.Dispose();
                executor.Embeds.Clear();

                return sb.ToString().Trim();
            }
            finally
            {
                _gate.Release();
            }
        }

        // \-\- Helpers \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        private static byte[] CaptureElementToPng(AutomationElement element)
        {
            using var capture = FlaUICapture.Element(element);
            using var bitmap  = new Bitmap(capture.Bitmap);
            using var ms      = new MemoryStream();
            bitmap.Save(ms, SysImaging.ImageFormat.Png);
            return ms.ToArray();
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized || _model == null || _context == null || _clipModel == null)
                throw new InvalidOperationException(
                    "MtmdHelper is not initialized. Call InitializeAsync() first.");
        }

        private void EnsureVision()
        {
            if (!SupportsVision)
                throw new InvalidOperationException("This model does not support vision inputs.");
        }

        private void EnsureAudio()
        {
            if (!SupportsAudio)
                throw new InvalidOperationException("This model does not support audio inputs.");
        }

        // \-\- Chat formatting (unchanged from original) \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        private static string FormatChatHistory(LLamaWeights model, ChatHistory history, bool addAssistant)
        {
            var template = new LLamaTemplate(model.NativeHandle)
            {
                AddAssistant = addAssistant,
            };

            foreach (var message in history.Messages)
                template.Add(message.AuthorRole.ToString().ToLowerInvariant(), message.Content);

            return LLamaTemplate.Encoding.GetString(template.Apply());
        }

        // \-\- IDisposable \-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-\-

        public void Dispose()
        {
            _clipModel?.Dispose();
            _context?.Dispose();
            _model?.Dispose();
            _gate.Dispose();
            IsInitialized = false;
        }
    }

    /// <summary>Configuration options for MtmdHelper.</summary>
    public class MtmdOptions
    {
        /// <summary>Use GPU for the multimodal projector. Default: false.</summary>
        public bool UseGpu { get; set; } = false;

        /// <summary>CPU thread count for model and MTMD inference. 0 = use default (9).</summary>
        public int Threads { get; set; } = 0;

        /// <summary>Maximum tokens to generate per response. Default: 4096.</summary>
        public int MaxTokens { get; set; } = 4096;

        /// <summary>Sampling temperature (0 = deterministic). Default: 0.1</summary>
        public float Temperature { get; set; } = 0.1f;

        /// <summary>KV cache / context size. 0 = use default (8192).</summary>
        public uint ContextSize { get; set; } = 0;
    }
}
