using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using UnityEngine;
using UnityEngine.Networking;

public sealed class Main : MonoBehaviour
{
    [SerializeField]
    MicrosoftSpeechToTextOptions _ApiOptions;

    [SerializeField]
    ClipProcessOptions _ClipOptions;

    bool _guiInitialized;
    string _status;
    Vector2 _scrollPos;
    AudioClip _recordClip;
    bool _pressRecord;
    bool _drawConfig;
    float _recordBeginTime;
    Task _analyzeTask;

    void Start() => LoadPrefs();

    void OnGUI()
    {
        if (!_guiInitialized)
        {
            var side = Screen.width;
            GUI.skin.button.fontSize = side / 22;
            GUI.skin.textField.fontSize = side / 22;
            GUI.skin.box.fontSize = side / 20;
            GUI.skin.label.fontSize = side / 20;
            GUI.skin.toggle.fontSize = side / 20;
            GUI.skin.toggle.border = new RectOffset(0, 0, 0, 0);
            GUI.skin.toggle.overflow = new RectOffset(0, 0, 0, 0);
            GUI.skin.toggle.imagePosition = ImagePosition.ImageOnly;
            GUI.skin.toggle.padding.right = 0;
            GUI.skin.toggle.padding.bottom = 0;
            GUI.skin.toggle.padding.left = side / 20;
            GUI.skin.toggle.padding.top = side / 20;
            GUI.skin.horizontalSlider.fixedHeight = side / 22;
            GUI.skin.horizontalSliderThumb.fixedWidth = side / 20;
            GUI.skin.horizontalSliderThumb.fixedHeight = side / 20;
            _guiInitialized = true;
        }

        bool pressTest = false, pressGotoKeyDoc = false, pressConfig = false, pressGotoAiStt = false;
        bool pressClear = false, pressCancel = false, pressApply = false;
        using (new GUILayout.VerticalScope(GUILayout.Width(Screen.width), GUILayout.Height(Screen.height)))
        {
            using (var scrollView = new GUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollView.scrollPosition;
                GUILayout.Label(_status);
            }
            if (_drawConfig)
            {
                using (new GUILayout.VerticalScope("Clip 選項", GUI.skin.box))
                {
                    GUILayout.Space(GUI.skin.box.fontSize);
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("儲存錄音: ", GUILayout.ExpandWidth(false));
                        _ClipOptions.SaveWav = GUILayout.Toggle(_ClipOptions.SaveWav, string.Empty, GUILayout.ExpandWidth(false));
                    }
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("錄音音量: x" + _ClipOptions.AmplifyVolumeRatio.ToString("N1"), GUILayout.ExpandWidth(false));
                        _ClipOptions.AmplifyVolumeRatio = GUILayout.HorizontalSlider(_ClipOptions.AmplifyVolumeRatio, 1, 10, GUILayout.ExpandWidth(true));
                    }
                }
                using (new GUILayout.VerticalScope("API 選項", GUI.skin.box))
                {
                    GUILayout.Space(GUI.skin.box.fontSize);
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("LOG: ", GUILayout.ExpandWidth(false));
                        _ApiOptions.LogProcess = GUILayout.Toggle(_ApiOptions.LogProcess, string.Empty, GUILayout.ExpandWidth(false));
                    }
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("區域: ", GUILayout.ExpandWidth(false));
                        _ApiOptions.Region = GUILayout.TextField(_ApiOptions.Region, GUILayout.ExpandWidth(true));
                    }
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("語言: ", GUILayout.ExpandWidth(false));
                        _ApiOptions.Language = GUILayout.TextField(_ApiOptions.Language, GUILayout.ExpandWidth(true));
                    }
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("金鑰: ", GUILayout.ExpandWidth(false));
                        _ApiOptions.SubscriptionKey = GUILayout.TextField(_ApiOptions.SubscriptionKey, GUILayout.ExpandWidth(true));
                    }
                    pressGotoAiStt = GUILayout.Button("取得金鑰 (Azure Speech Service)");
                }
                using (new GUILayout.HorizontalScope())
                {
                    pressCancel = GUILayout.Button("取消");
                    pressApply = GUILayout.Button("套用");
                }
            }
            else
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (string.IsNullOrEmpty(_ApiOptions.SubscriptionKey))
                    {
                        if (string.IsNullOrWhiteSpace(_status))
                            _status = "未設定訂閱金鑰" + Environment.NewLine;

                        _pressRecord = false;
                        pressGotoKeyDoc = GUILayout.Button("關於訂閱金鑰", GUILayout.ExpandWidth(true));
                        pressConfig = GUILayout.Button("設定", GUILayout.ExpandWidth(false));
                    }
                    else
                    {
                        GUI.enabled = _analyzeTask == null || _analyzeTask.IsCompleted;
                        if (Microphone.devices?.Length > 0)
                        {
                            if (GUILayout.RepeatButton("按住錄製 (放開後分析)", GUILayout.ExpandWidth(true)))
                                _pressRecord = true;
                            else if (Event.current.type == EventType.Repaint)
                                _pressRecord = false;
                        }
                        else
                            _pressRecord = false;
                        pressTest = GUILayout.Button("測試", GUILayout.ExpandWidth(false));
                        pressConfig = GUILayout.Button("設定", GUILayout.ExpandWidth(false));
                        GUI.enabled = true;
                    }
                }
                pressClear = GUILayout.Button("清除");
            }
            GUILayout.Space(GUI.skin.button.fontSize);
        }

        if (_pressRecord)
        {
            if (_recordClip == null)
            {
                _status += "(開始錄製 ...)" + Environment.NewLine;
                _recordClip = Microphone.Start(deviceName: null, loop: true, lengthSec: 3599, frequency: AudioSettings.outputSampleRate);
                _recordBeginTime = Time.realtimeSinceStartup;
            }
        }
        else
        {
            if (_recordClip != null)
            {
                var duration = Time.realtimeSinceStartup - _recordBeginTime;
                Microphone.End(deviceName: null);
                AudioClip clip = null;
                try
                {
                    clip = _recordClip.Clip(duration, _ClipOptions.AmplifyVolumeRatio);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex.Message);
                    Debug.LogException(ex);
                    _status += "錯誤: " + ex.Message + Environment.NewLine;
                }
                Destroy(_recordClip);
                _recordClip = null;
                _recordBeginTime = 0;

                if (clip != null)
                    _analyzeTask = RecordThenAnalyzeAsync(clip).AsTask();
            }
        }

        if (pressGotoKeyDoc)
            Application.OpenURL("https://learn.microsoft.com/zh-tw/azure/ai-services/multi-service-resource?pivots=azportal&tabs=linux#get-the-keys-for-your-resource");

        if (pressGotoAiStt)
            Application.OpenURL("https://portal.azure.com/#view/Microsoft_Azure_ProjectOxford/CognitiveServicesHub/~/SpeechServices");

        if (pressTest)
            _analyzeTask = RunTestAsync().AsTask();

        if (pressClear)
            _status = string.Empty;

        if (pressCancel)
        {
            LoadPrefs();
            _drawConfig = false;
        }

        if (pressConfig)
            _drawConfig = true;

        if (pressApply)
        {
            SavePrefs();
            _drawConfig = false;
        }
    }

    void LoadPrefs()
    {
        JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(nameof(_ApiOptions), "{}"), _ApiOptions);
        JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(nameof(_ClipOptions), "{}"), _ClipOptions);
    }

    void SavePrefs()
    {
        PlayerPrefs.SetString(nameof(_ClipOptions), JsonUtility.ToJson(_ClipOptions));
        PlayerPrefs.SetString(nameof(_ApiOptions), JsonUtility.ToJson(_ApiOptions));
    }

    async ValueTask RecordThenAnalyzeAsync(AudioClip clip, CancellationToken cancellationToken = default)
    {
        Debug.LogFormat("{0}: {1}", nameof(RecordThenAnalyzeAsync), new { clip.length, clip.samples, clip.frequency, clip.channels });
        try
        {
            _status += "(開始分析 ...)" + Environment.NewLine;
            var stream = clip.ToWavStream();

            if (_ClipOptions.SaveWav)
            {
                var fileName = Application.persistentDataPath + $"/{DateTime.Now:yyMMddHHmmss}.wav";
                using (var output = File.OpenWrite(fileName))
                {
                    await stream.CopyToAsync(output);
                    await output.FlushAsync();
                }
                stream.Position = 0;
                _status += "存檔: " + fileName + Environment.NewLine;
            }

            var api = new MicrosoftSpeechToText(_ApiOptions);
            var format = AudioStreamFormat.GetWaveFormatPCM(
                samplesPerSecond: (uint)clip.frequency,
                bitsPerSample: 16, // TODO: 需要調查有無確切算法
                channels: (byte)clip.channels);
            await foreach (var result in api.AnalyzeAsync(stream, format, cancellationToken))
                _status += result + Environment.NewLine;
            _status += "(完成分析)" + Environment.NewLine;
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
            Debug.LogException(ex);
            _status += "錯誤: " + ex.Message + Environment.NewLine;
        }
    }

    async ValueTask RunTestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _status += "(開始測試分析 ...)" + Environment.NewLine;
            var url = Application.streamingAssetsPath + "/test.wav";
            if (!url.Contains("://"))
                url = "file://" + url;

            var request = UnityWebRequest.Get(url);
            var send = request.SendWebRequest();
            while (!send.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                await Task.Yield();
            }

            var data = request.downloadHandler.data;
            if (data == null || data.Length == 0)
                throw new Exception("data fault");
            var stream = new MemoryStream(data);
            var api = new MicrosoftSpeechToText(_ApiOptions);
            await foreach (var result in api.AnalyzeAsync(stream, cancellationToken: cancellationToken))
                _status += result + Environment.NewLine;
            _status += "(完成測試分析)" + Environment.NewLine;
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
            Debug.LogException(ex);
            _status += "錯誤: " + ex.Message + Environment.NewLine;
        }
    }
}

[Serializable]
public sealed class ClipProcessOptions
{
    [field: SerializeField]
    public bool SaveWav { get; set; }

    [field: SerializeField]
    public float AmplifyVolumeRatio { get; set; } = 3;
}

[Serializable]
public sealed class MicrosoftSpeechToTextOptions
{
    [field: SerializeField]
    public bool LogProcess { get; set; }

    [field: SerializeField]
    public string Language { get; set; } = "zh-TW";

    [field: SerializeField]
    public string Region { get; set; } = "eastasia";

    // 取得金要請參閱 https://learn.microsoft.com/zh-tw/azure/ai-services/multi-service-resource?pivots=azportal&tabs=linux#get-the-keys-for-your-resource
    [field: SerializeField]
    public string SubscriptionKey { get; set; }
}

public sealed class MicrosoftSpeechToText
{
    MicrosoftSpeechToTextOptions _options;

    public MicrosoftSpeechToText(MicrosoftSpeechToTextOptions options) => _options = options;

    public async IAsyncEnumerable<string> AnalyzeAsync(
        Stream stream,
        AudioStreamFormat format = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Debug.Log(nameof(AnalyzeAsync));

        using var pushAudioStream = format == null ? AudioInputStream.CreatePushStream() : AudioInputStream.CreatePushStream(format);
        var analyze = AnalyzeAsync(pushAudioStream, cancellationToken);
        _ = WriteToAsync(stream, pushAudioStream, cancellationToken);

        await foreach (var result in analyze.WithCancellation(cancellationToken))
            yield return result;

        Debug.LogFormat("{0} end", nameof(AnalyzeAsync));
    }

    async ValueTask WriteToAsync(Stream source, PushAudioInputStream target, CancellationToken cancellationToken)
    {
        var batch = new byte[4096];
        while (await source.ReadAsync(batch, cancellationToken) is int read && read > 0)
        {
            target.Write(batch, read);
            // Debug.LogFormat("載入檔案: {0:P}", (float)source.Position / source.Length);
        }
        Debug.LogFormat("載入檔案完成, 解析中 {0} bytes ...", source.Position);
        target.Close();
    }

    public async IAsyncEnumerable<string> AnalyzeAsync(
        AudioInputStream audioStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Debug.LogFormat("{0} a begin", nameof(AnalyzeAsync));

        byte emptyCount = 0;

        var speechConfig = SpeechConfig.FromSubscription(_options.SubscriptionKey, _options.Region);
        speechConfig.OutputFormat = OutputFormat.Detailed;
        if (_options.LogProcess)
            speechConfig.SetProperty(PropertyId.Speech_LogFilename, Application.persistentDataPath + "/speech.log");
        using var audioConfig = AudioConfig.FromStreamInput(audioStream);
        if (_options.LogProcess)
            audioConfig.SetProperty(PropertyId.Speech_LogFilename, Application.persistentDataPath + "/audio.log");
        using var recognizer = new SpeechRecognizer(speechConfig, _options.Language, audioConfig);

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(continueOnCapturedContext: false);
            if (result.Reason == ResultReason.Canceled)
            {
                var details = CancellationDetails.FromResult(result);
                if (details.Reason == CancellationReason.Error)
                    throw new Exception($"{details.ErrorCode}: {details.ErrorDetails}");
                Debug.Log(new { result.Reason, details = new { details.Reason, details.ErrorCode, details.ErrorDetails } });
                break;
            }
            var content = result.Text;
            if (string.IsNullOrWhiteSpace(content))
            {
                if (++emptyCount > 6)
                {
                    Debug.LogFormat("{0} break with result empty 6 times", nameof(AnalyzeAsync));
                    break;
                }
                Debug.LogFormat("{0} continue with result empty", nameof(AnalyzeAsync));
                continue;
            }
            content = content.Replace("，", Environment.NewLine);
            content = content.Replace("。", Environment.NewLine);
            content = content.Replace("？", Environment.NewLine);
            content = content.Replace("！", Environment.NewLine);
            var start = TimeSpan.FromTicks(result.OffsetInTicks);
            var duration = result.Duration;
            var durationOfChars = duration / content.Length;
            foreach (var line in content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                var end = start + durationOfChars * line.Length;
                Debug.LogFormat(line);
                yield return line;
                start = end;
            }
        }
        Debug.LogFormat("{0} a end", nameof(AnalyzeAsync));
    }
}

public static class AudioClipUtility
{
    public static AudioClip Clip(this AudioClip clip, float maxTime, float amplify = 1)
    {
        var newLength = Mathf.FloorToInt(maxTime * clip.frequency);
        var trimmedSamples = new float[newLength];

        clip.GetData(trimmedSamples, 0);

        if (amplify != 1)
        {
            for (var i = 0; i < newLength; i++)
                trimmedSamples[i] *= amplify;
        }

        var trimmedClip = AudioClip.Create("Trimmed", newLength, clip.channels, clip.frequency, stream: false);
        trimmedClip.SetData(trimmedSamples, 0);

        return trimmedClip;
    }

    public static MemoryStream ToWavStream(this AudioClip clip)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        var intData = new short[samples.Length];
        for (int i = 0; i < samples.Length; i++)
            intData[i] = (short)(samples[i] * short.MaxValue);

        byte[] header = CreateWavHeader(clip);

        var wavData = new byte[header.Length + intData.Length * 2];
        Buffer.BlockCopy(header, 0, wavData, 0, header.Length);
        Buffer.BlockCopy(intData, 0, wavData, header.Length, intData.Length * 2);

        return new MemoryStream(wavData);
    }

    static byte[] CreateWavHeader(AudioClip audioClip)
    {
        // ref: https://docs.fileformat.com/audio/wav/

        var numChannels = (ushort)audioClip.channels;
        int sampleRate = audioClip.frequency;
        ushort bitDepth = 16;

        var header = new byte[44];

        header[0] = (byte)'R';
        header[1] = (byte)'I';
        header[2] = (byte)'F';
        header[3] = (byte)'F';

        int fileSize = audioClip.samples * numChannels * bitDepth / 8 + 44 - 8;
        header[4] = (byte)(fileSize & 0xFF);
        header[5] = (byte)((fileSize >> 8) & 0xFF);
        header[6] = (byte)((fileSize >> 16) & 0xFF);
        header[7] = (byte)((fileSize >> 24) & 0xFF);

        header[8] = (byte)'W';
        header[9] = (byte)'A';
        header[10] = (byte)'V';
        header[11] = (byte)'E';

        header[12] = (byte)'f';
        header[13] = (byte)'m';
        header[14] = (byte)'t';
        header[15] = (byte)' ';

        header[16] = 16;
        header[17] = 0;
        header[18] = 0;
        header[19] = 0;

        header[20] = 1;
        header[21] = 0;

        header[22] = (byte)numChannels;
        header[23] = 0;

        header[24] = (byte)(sampleRate & 0xFF);
        header[25] = (byte)((sampleRate >> 8) & 0xFF);
        header[26] = (byte)((sampleRate >> 16) & 0xFF);
        header[27] = (byte)((sampleRate >> 24) & 0xFF);

        int byteRate = sampleRate * numChannels * bitDepth / 8;
        header[28] = (byte)(byteRate & 0xFF);
        header[29] = (byte)((byteRate >> 8) & 0xFF);
        header[30] = (byte)((byteRate >> 16) & 0xFF);
        header[31] = (byte)((byteRate >> 24) & 0xFF);

        header[32] = (byte)(numChannels * bitDepth / 8);
        header[33] = (byte)((bitDepth >> 8) & 0xFF);

        header[34] = (byte)(bitDepth & 0xFF);
        header[35] = 0;

        header[36] = (byte)'d';
        header[37] = (byte)'a';
        header[38] = (byte)'t';
        header[39] = (byte)'a';

        int dataSize = audioClip.samples * numChannels * bitDepth / 8;
        header[40] = (byte)(dataSize & 0xFF);
        header[41] = (byte)((dataSize >> 8) & 0xFF);
        header[42] = (byte)((dataSize >> 16) & 0xFF);
        header[43] = (byte)((dataSize >> 24) & 0xFF);

        return header;
    }
}
