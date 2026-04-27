using LLama;
using LLama.Common;
using LLama.Exceptions;
using LLama.Native;
using LLama.Sampling;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
 
namespace ApexComputerUse
{
    public class MtmdInteractiveModeExecute
    {
        public static async Task RunComputerUseMode(string modelPath , string multiModalProj)
        {
            const int maxTokens = 4096;

            var parameters = new ModelParams(modelPath);

            var mtmdParameters = MtmdContextParams.Default();
            mtmdParameters.UseGpu = false;

            using var model = await LLamaWeights.LoadFromFileAsync(parameters);
            using var context = model.CreateContext(parameters);

            // Mtmd Init
            using var clipModel = await MtmdWeights.LoadFromFileAsync(multiModalProj, model, mtmdParameters);

            var mediaMarker = mtmdParameters.MediaMarker ?? NativeApi.MtmdDefaultMarker() ?? "<media>";
            var supportsVision = clipModel.SupportsVision;
            var supportsAudio = clipModel.SupportsAudio;

            var ex = new InteractiveExecutor(context, clipModel);
            var chatHistory = new ChatHistory();

            Debug.WriteLine("The executor has been enabled. In this example the maximum tokens is set to {0} and the context size is {1}.", maxTokens, parameters.ContextSize);
            Debug.WriteLine("Model: {0}", modelPath);
            Debug.WriteLine("MMProj: {0}", multiModalProj);
            Debug.WriteLine("Supported modalities: vision={0} | audio={1} | video=no", supportsVision ? "yes" : "no", supportsAudio ? "yes" : "no");
            if (supportsVision)
                Debug.WriteLine("To send an image, enter its filename in double braces, like this {{c:/image.jpg}}.");
            if (supportsAudio)
                Debug.WriteLine("To send audio, enter its filename in double braces, like this {{c:/audio.wav}}.");
            Debug.WriteLine("Video inputs are not supported (format would be {{c:/video.mp4}}).");
            Debug.WriteLine("Commands: /exit (return to main menu) | /clear (reset the chat history and KV cache).");
            Debug.WriteLine("Press Ctrl+c to return to main menu.");

            void ResetConversation()
            {
                context.NativeHandle.MemoryClear();
                foreach (var embed in ex.Embeds)
                    embed.Dispose();
                ex.Embeds.Clear();
                clipModel.ClearMedia();
                chatHistory.Messages.Clear();
                ex = new InteractiveExecutor(context, clipModel);
                Debug.WriteLine("User:");
            }

            var inferenceParams = new InferenceParams
            {
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = 0.1f
                },

                AntiPrompts = new List<string> { "User:" },
                MaxTokens = maxTokens

            };

            do
            {
                Debug.Write("User: ");
                string? prompt = Console.ReadLine();
                if (prompt is null || prompt.Equals("/exit", StringComparison.OrdinalIgnoreCase))
                    break;
                if (prompt.Equals("/clear", StringComparison.OrdinalIgnoreCase))
                {
                    ResetConversation();
                    continue;
                }

                var userPrompt = prompt;

                // Evaluate if we have media
                //
                var mediaMatches = Regex.Matches(userPrompt, @"\{\{(.*?)\}\}").Select(m => m.Value);
                var mediaCount = mediaMatches.Count();
                var hasMedia = mediaCount > 0;

                if (hasMedia)
                {
                    var mediaPathsWithCurlyBraces = Regex.Matches(userPrompt, @"\{\{(.*?)\}\}").Select(m => m.Value);
                    var mediaPaths = Regex.Matches(userPrompt, @"\{\{(.*?)\}\}").Select(m => m.Groups[1].Value).ToList();

                    var embeds = new List<SafeMtmdEmbed>();
                    var imageList = new List<byte[]>();
                    var audioList = new List<string>();
                    var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ".png",
                        ".jpg",
                        ".jpeg",
                        ".bmp",
                        ".gif",
                        ".webp"
                    };
                    var audioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ".wav",
                        ".mp3",
                        ".flac",
                        ".ogg",
                        ".m4a",
                        ".aac",
                        ".opus"
                    };
                    var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ".mp4",
                        ".mkv",
                        ".mov",
                        ".avi",
                        ".webm"
                    };

                    try
                    {
                        foreach (var mediaPath in mediaPaths)
                        {
                            var extension = Path.GetExtension(mediaPath);
                            var isImage = !string.IsNullOrEmpty(extension) && imageExtensions.Contains(extension);
                            var isAudio = !string.IsNullOrEmpty(extension) && audioExtensions.Contains(extension);
                            var isVideo = !string.IsNullOrEmpty(extension) && videoExtensions.Contains(extension);

                            if (isVideo)
                                throw new NotSupportedException("Video inputs are not supported by MTMD.");
                            if (isImage && !supportsVision)
                                throw new InvalidOperationException("This model does not support vision inputs.");
                            if (isAudio && !supportsAudio)
                                throw new InvalidOperationException("This model does not support audio inputs.");

                            if (isImage)
                            {
                                // Keep the raw image data so the caller can reuse or inspect the images later.
                                imageList.Add(File.ReadAllBytes(mediaPath));
                            }
                            else if (isAudio)
                            {
                                audioList.Add(mediaPath);
                            }

                            var embed = clipModel.LoadMedia(mediaPath);
                            embeds.Add(embed);
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or RuntimeError or NotSupportedException or InvalidOperationException)
                    {
                        Debug.Write(
                            $"Could not load your {(mediaCount == 1 ? "media" : "medias")}:");
                        Debug.Write($"{exception.Message}");
                        Debug.WriteLine("Please try again.");
                        clipModel.ClearMedia();
                        break;
                    }


                    // Replace placeholders with media markers (one marker per image)
                    foreach (var path in mediaPathsWithCurlyBraces)
                    {
                        userPrompt = userPrompt.Replace(path, mediaMarker, StringComparison.Ordinal);
                    }


                    if (audioList.Count > 0)
                    {
                        Debug.WriteLine($"Loaded audio files: {string.Join(", ", audioList)}");
                    }


                    // Initialize Images in executor
                    //
                    ex.Embeds.Clear();
                    foreach (var embed in embeds)
                        ex.Embeds.Add(embed);
                }

                var formattedPrompt = BuildChatDelta(model, chatHistory, userPrompt);

                var responseBuilder = new StringBuilder();
                await foreach (var text in ex.InferAsync(formattedPrompt, inferenceParams))
                {
                    Debug.Write(text);
                    responseBuilder.Append(text);
                }
                Debug.Write(" ");
                chatHistory.AddMessage(AuthorRole.Assistant, responseBuilder.ToString());

            }
            while (true);
        }

        private static string BuildChatDelta(LLamaWeights model, ChatHistory history, string userContent)
        {
            var pastPrompt = FormatChatHistory(model, history, addAssistant: false);
            history.AddMessage(AuthorRole.User, userContent);
            var fullPrompt = FormatChatHistory(model, history, addAssistant: true);

            if (!fullPrompt.StartsWith(pastPrompt, StringComparison.Ordinal))
                return fullPrompt;

            var delta = fullPrompt[pastPrompt.Length..];
            if (pastPrompt.Length > 0 && pastPrompt.EndsWith('\n') && (delta.Length == 0 || delta[0] != '\n'))
                delta = "\n" + delta;

            return delta;
        }

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
    }
}
