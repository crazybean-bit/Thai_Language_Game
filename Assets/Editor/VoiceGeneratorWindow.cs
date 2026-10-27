using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ThaiMystery.Editor
{
    // Thai Mystery > Generate Voices: creates spoken Thai clips for every line and word
    // with Azure text-to-speech, straight from words.json and dialogue.json.
    // The key is kept in this machine's EditorPrefs, never in the project files.
    //
    // Clips made:
    //   Lines/<dialogue>_<index>   lines a character says that the protagonist can make out
    //   Words/<key>                every word, in the voice of whoever teaches the words (Mali)
    //   Words/Own/<key>            every word in the protagonist's own voice, for learned words
    public class VoiceGeneratorWindow : EditorWindow
    {
        private const string Prefs = "ThaiMystery.Voices.";
        private const string AudioRoot = "Assets/Resources/Audio";
        private const string TestPath = "Temp/ThaiVoiceTest.wav";
        private const double MinSecondsBetweenRequests = 0.4;

        [Serializable]
        private class SpeakerVoice
        {
            public string speaker;
            public string voice;
            public string pitch;   // e.g. "+8%", empty for the voice's own pitch
            public string rate;    // e.g. "-5%", empty for normal speed
        }

        [Serializable]
        private class SpeakerVoiceList
        {
            public List<SpeakerVoice> items = new List<SpeakerVoice>();
        }

        private class Job
        {
            public string path;
            public string text;
            public SpeakerVoice voice;
            public string rate;    // overrides voice.rate when set
            public string group;   // "line:<speaker>", "words" or "own"
        }

        private string apiKey = "";
        private string region = "southeastasia";
        private string wordSpeaker = "มะลิ";
        private string testText = "คุณตื่นแล้ว ฉันชื่อมะลิ คุณหิวไหม";
        private bool slowWords = true;
        private SpeakerVoice defaultCharacter = new SpeakerVoice { speaker = "", voice = "th-TH-PremwadeeNeural" };
        private SpeakerVoice ownVoice = new SpeakerVoice { speaker = "", voice = "th-TH-NiwatNeural" };
        private SpeakerVoiceList speakers = new SpeakerVoiceList();

        private readonly Queue<Job> queue = new Queue<Job>();
        private UnityWebRequest request;
        private Job running;
        private int total;
        private int finished;
        private double nextRequestTime;
        [SerializeField] private string status = "";
        private Vector2 scroll;

        private UnityWebRequest testRequest;

        [MenuItem("Thai Mystery/Generate Voices...")]
        public static void Open() => GetWindow<VoiceGeneratorWindow>("Thai Voices");

        private void OnEnable()
        {
            apiKey = EditorPrefs.GetString(Prefs + "Key", "");
            region = EditorPrefs.GetString(Prefs + "Region", region);
            wordSpeaker = EditorPrefs.GetString(Prefs + "WordSpeaker", wordSpeaker);
            testText = EditorPrefs.GetString(Prefs + "TestText", testText);
            slowWords = EditorPrefs.GetBool(Prefs + "SlowWords", true);
            LoadVoice(Prefs + "DefaultVoice", ref defaultCharacter);
            LoadVoice(Prefs + "OwnVoice", ref ownVoice);
            // Older versions kept only a voice name for other characters.
            if (!EditorPrefs.HasKey(Prefs + "DefaultVoice"))
                defaultCharacter.voice = EditorPrefs.GetString(Prefs + "Character", defaultCharacter.voice);

            var saved = EditorPrefs.GetString(Prefs + "Speakers", "");
            if (!string.IsNullOrEmpty(saved)) speakers = JsonUtility.FromJson<SpeakerVoiceList>(saved);
            if (speakers.items.Count == 0)
            {
                speakers.items.Add(new SpeakerVoice { speaker = "มะลิ", voice = "th-TH-AcharaNeural", pitch = "+6%", rate = "-5%" });
                speakers.items.Add(new SpeakerVoice { speaker = "???", voice = "th-TH-PremwadeeNeural" });
            }
        }

        private static void LoadVoice(string key, ref SpeakerVoice voice)
        {
            var saved = EditorPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(saved)) voice = JsonUtility.FromJson<SpeakerVoice>(saved);
        }

        // Unity reloads scripts when entering Play mode, which disables this window.
        // Only report an interruption if a run was actually in progress.
        private void OnDisable()
        {
            if (IsRunning) StopAll($"Interrupted after {finished} of {total} clips. Click Generate missing clips to finish the rest.");
            EditorApplication.update -= TestTick;
            testRequest?.Dispose();
            testRequest = null;
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(Prefs + "Key", apiKey);
            EditorPrefs.SetString(Prefs + "Region", region);
            EditorPrefs.SetString(Prefs + "WordSpeaker", wordSpeaker);
            EditorPrefs.SetString(Prefs + "TestText", testText);
            EditorPrefs.SetBool(Prefs + "SlowWords", slowWords);
            EditorPrefs.SetString(Prefs + "DefaultVoice", JsonUtility.ToJson(defaultCharacter));
            EditorPrefs.SetString(Prefs + "OwnVoice", JsonUtility.ToJson(ownVoice));
            EditorPrefs.SetString(Prefs + "Speakers", JsonUtility.ToJson(speakers));
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "Creates Thai voice clips for the lines characters say, and for every word in words.json in two voices: " +
                "the character who teaches them, and the protagonist's own voice for words he has learned. " +
                "Paste KEY 1 and the region from your Speech resource's 'Keys and Endpoint' page. " +
                "The key is stored only on this computer.", MessageType.Info);

            bool canCall = !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(region);
            using (new EditorGUI.DisabledScope(IsRunning))
            {
                EditorGUILayout.LabelField("Azure Speech", EditorStyles.boldLabel);
                apiKey = EditorGUILayout.PasswordField("Key", apiKey);
                region = EditorGUILayout.TextField("Region", region);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Voices", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Thai voices: th-TH-PremwadeeNeural (female), th-TH-AcharaNeural (female), th-TH-NiwatNeural (male). " +
                    "Pitch and speed take values like +8% or -5%. Try voices for free in Azure Speech Studio's voice gallery.",
                    EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Character", EditorStyles.miniLabel, GUILayout.Width(120));
                EditorGUILayout.LabelField("Voice", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Pitch", EditorStyles.miniLabel, GUILayout.Width(50));
                EditorGUILayout.LabelField("Speed", EditorStyles.miniLabel, GUILayout.Width(50));
                GUILayout.Space(176);
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < speakers.items.Count; i++)
                {
                    var s = speakers.items[i];
                    EditorGUILayout.BeginHorizontal();
                    s.speaker = EditorGUILayout.TextField(s.speaker, GUILayout.Width(120));
                    VoiceRow(s, canCall, "Redo lines", () => Generate(j => j.group == "line:" + s.speaker));
                    bool remove = GUILayout.Button("x", GUILayout.Width(22));
                    EditorGUILayout.EndHorizontal();
                    if (remove) { speakers.items.RemoveAt(i); break; }
                }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Anyone else", GUILayout.Width(120));
                VoiceRow(defaultCharacter, canCall, null, null);
                GUILayout.Space(26);
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("Add character voice", GUILayout.Width(160)))
                    speakers.items.Add(new SpeakerVoice { speaker = "", voice = defaultCharacter.voice });

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Words", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                wordSpeaker = EditorGUILayout.TextField(new GUIContent("Taught by", "Whose voice the protagonist remembers new words in"), wordSpeaker);
                using (new EditorGUI.DisabledScope(!canCall))
                {
                    if (GUILayout.Button("Redo words", GUILayout.Width(100))) Generate(j => j.group == "words");
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Own voice", "The protagonist, once he has learned a word"), GUILayout.Width(120));
                VoiceRow(ownVoice, canCall, "Redo words", () => Generate(j => j.group == "own"));
                GUILayout.Space(26);
                EditorGUILayout.EndHorizontal();
                slowWords = EditorGUILayout.Toggle(new GUIContent("Slower single words", "Reads single words 20% slower, easier for learners"), slowWords);

                EditorGUILayout.Space();
                testText = EditorGUILayout.TextField(new GUIContent("Test sentence", "What the Test buttons say"), testText);
            }

            EditorGUILayout.Space();
            if (!IsRunning)
            {
                using (new EditorGUI.DisabledScope(!canCall))
                {
                    if (GUILayout.Button("Generate missing clips", GUILayout.Height(30))) Generate(null);
                    if (GUILayout.Button("Redo everything") &&
                        EditorUtility.DisplayDialog("Redo every clip?", "This remakes every voice clip, which uses up more of Azure's free allowance.", "Redo all", "Cancel"))
                        Generate(j => true);
                }
            }
            else
            {
                var rect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(rect, total == 0 ? 0 : (float)finished / total, $"{finished} / {total}");
                if (GUILayout.Button("Cancel")) StopAll("Cancelled.");
            }

            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        // Voice, pitch and speed fields plus Test and an optional redo button.
        private void VoiceRow(SpeakerVoice v, bool canCall, string redoLabel, Action redo)
        {
            v.voice = EditorGUILayout.TextField(v.voice);
            v.pitch = EditorGUILayout.TextField(v.pitch, GUILayout.Width(50));
            v.rate = EditorGUILayout.TextField(v.rate, GUILayout.Width(50));
            using (new EditorGUI.DisabledScope(!canCall || testRequest != null))
            {
                if (GUILayout.Button("Test", GUILayout.Width(50))) Test(v);
            }
            using (new EditorGUI.DisabledScope(!canCall || redo == null))
            {
                if (redo != null && GUILayout.Button(redoLabel, GUILayout.Width(100))) redo();
                else if (redo == null) GUILayout.Space(104);
            }
        }

        private bool IsRunning => running != null || queue.Count > 0;

        // redo == null makes only missing clips; otherwise it also remakes the clips it picks.
        private void Generate(Func<Job, bool> redo)
        {
            SavePrefs();
            queue.Clear();
            var jobs = CollectJobs();
            foreach (var job in jobs)
                if ((redo != null && redo(job)) || !File.Exists(job.path)) queue.Enqueue(job);

            total = queue.Count;
            finished = 0;
            if (total == 0)
            {
                status = $"All {jobs.Count} clips already exist. Use a Redo button to make some again.";
                return;
            }
            status = $"Generating {total} clips...";
            nextRequestTime = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private List<Job> CollectJobs()
        {
            var jobs = new List<Job>();
            var dialogue = JsonUtility.FromJson<DialogueList>(File.ReadAllText("Assets/Resources/Data/dialogue.json"));
            var words = JsonUtility.FromJson<WordList>(File.ReadAllText("Assets/Resources/Data/words.json"));

            foreach (var d in dialogue.dialogues)
            {
                for (int i = 0; i < d.lines.Length; i++)
                {
                    var line = d.lines[i];
                    if (string.IsNullOrEmpty(line.text) || !WordBook.IsThai(line.text)) continue;
                    // The protagonist's reading and recall play word clips, and lines he can't
                    // make out are only noise, so neither needs a clip of its own.
                    if (string.IsNullOrEmpty(line.speaker)) continue;
                    if (string.IsNullOrEmpty(line.heard) && string.IsNullOrEmpty(d.heard)) continue;
                    jobs.Add(new Job
                    {
                        path = $"{AudioRoot}/Lines/{d.id}_{i}.wav",
                        // The game spaces words out for learners; spoken Thai runs them together.
                        text = line.text.Replace(" ", ""),
                        voice = VoiceFor(line.speaker),
                        group = "line:" + line.speaker
                    });
                }
            }
            string wordRate = slowWords ? "-20%" : null;
            foreach (var w in words.words)
            {
                string thai = w.thai.Trim();
                string key = VoicePlayer.WordKey(thai);
                jobs.Add(new Job { path = $"{AudioRoot}/Words/{key}.wav", text = thai, voice = VoiceFor(wordSpeaker), rate = wordRate, group = "words" });
                jobs.Add(new Job { path = $"{AudioRoot}/Words/Own/{key}.wav", text = thai, voice = ownVoice, rate = wordRate, group = "own" });
            }
            return jobs;
        }

        private SpeakerVoice VoiceFor(string speaker)
        {
            foreach (var s in speakers.items)
                if (s.speaker == speaker && !string.IsNullOrEmpty(s.voice)) return s;
            return defaultCharacter;
        }

        private void Tick()
        {
            if (request != null)
            {
                if (!request.isDone) return;
                HandleResponse();
                return;
            }

            if (queue.Count == 0)
            {
                EditorApplication.update -= Tick;
                AssetDatabase.Refresh();
                status = $"Done. Made {finished} clips in {AudioRoot}. Press Play to hear them.";
                running = null;
                Repaint();
                return;
            }

            if (EditorApplication.timeSinceStartup < nextRequestTime) return;
            running = queue.Dequeue();
            request = BuildRequest(running.text, running.voice, running.rate);
            request.SendWebRequest();
        }

        private UnityWebRequest BuildRequest(string text, SpeakerVoice voice, string rateOverride = null)
        {
            string rate = !string.IsNullOrEmpty(rateOverride) ? rateOverride : voice.rate;
            string prosody = Percent("rate", rate) + Percent("pitch", voice.pitch);
            string spoken = SecurityElement.Escape(text);
            // Azure rejects a <prosody> with nothing in it, so leave it out when nothing is changed.
            if (prosody.Length > 0) spoken = $"<prosody{prosody}>{spoken}</prosody>";
            string ssml =
                "<speak version=\"1.0\" xml:lang=\"th-TH\" xmlns=\"http://www.w3.org/2001/10/synthesis\">" +
                $"<voice name=\"{SecurityElement.Escape(voice.voice.Trim())}\">{spoken}</voice></speak>";

            var req = new UnityWebRequest($"https://{region.Trim()}.tts.speech.microsoft.com/cognitiveservices/v1", "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ssml)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 60
            };
            req.SetRequestHeader("Ocp-Apim-Subscription-Key", apiKey.Trim());
            req.SetRequestHeader("Content-Type", "application/ssml+xml");
            req.SetRequestHeader("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");
            return req;
        }

        // " pitch=\"+6%\"" from "+6%", "6%" or "6"; empty for blank or zero.
        private static string Percent(string attribute, string value)
        {
            value = (value ?? "").Trim().TrimEnd('%').Trim();
            if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float n) || n == 0)
                return "";
            return $" {attribute}=\"{(n > 0 ? "+" : "")}{n.ToString(System.Globalization.CultureInfo.InvariantCulture)}%\"";
        }

        private static string Hint(long code) =>
            code == 401 ? " Check the key and region match your Speech resource."
            : code == 400 ? " Check the voice names are spelled correctly, and pitch and speed look like +8% or -5%."
            : "";

        private void HandleResponse()
        {
            var req = request;
            request = null;

            if (req.result == UnityWebRequest.Result.Success)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(running.path));
                File.WriteAllBytes(running.path, req.downloadHandler.data);
                finished++;
                running = null;
                nextRequestTime = EditorApplication.timeSinceStartup + MinSecondsBetweenRequests;
            }
            else if (req.responseCode == 429)
            {
                // The free tier allows about 20 requests a minute: wait and retry the same clip.
                var retry = new Queue<Job>();
                retry.Enqueue(running);
                while (queue.Count > 0) retry.Enqueue(queue.Dequeue());
                while (retry.Count > 0) queue.Enqueue(retry.Dequeue());
                running = null;
                double wait = double.TryParse(req.GetResponseHeader("Retry-After"), out var s) ? s : 10;
                nextRequestTime = EditorApplication.timeSinceStartup + wait;
                status = $"Azure's free tier is rate limited, waiting {wait:0}s... ({finished}/{total} done)";
            }
            else
            {
                StopAll($"Failed on {running?.path}: {req.responseCode} {req.error}.{Hint(req.responseCode)}");
            }
            req.Dispose();
            Repaint();
        }

        private void StopAll(string message)
        {
            EditorApplication.update -= Tick;
            if (request != null)
            {
                request.Abort();
                request.Dispose();
                request = null;
            }
            queue.Clear();
            running = null;
            if (finished > 0) AssetDatabase.Refresh();
            status = message;
            Repaint();
        }

        // Test: speak the test sentence with one row's settings, without saving a clip.
        private void Test(SpeakerVoice voice)
        {
            SavePrefs();
            testRequest = BuildRequest(testText.Trim(), voice);
            testRequest.SendWebRequest();
            status = $"Testing {voice.voice} {voice.pitch} {voice.rate}...";
            EditorApplication.update -= TestTick;
            EditorApplication.update += TestTick;
        }

        private void TestTick()
        {
            if (testRequest == null || !testRequest.isDone) return;
            EditorApplication.update -= TestTick;
            var req = testRequest;
            testRequest = null;
            if (req.result == UnityWebRequest.Result.Success)
            {
                byte[] wav = req.downloadHandler.data;
                File.WriteAllBytes(TestPath, wav);
                if (PlayPreview(wav)) status = "Playing the test sentence.";
                else
                {
                    EditorUtility.OpenWithDefaultApp(Path.GetFullPath(TestPath));
                    status = "Opened the test sentence in your audio player.";
                }
            }
            else
            {
                status = $"Test failed: {req.responseCode} {req.error}.{Hint(req.responseCode)}";
            }
            req.Dispose();
            Repaint();
        }

        // Unity has no public API to play a sound in the editor; its own preview player is internal.
        private static bool PlayPreview(byte[] wav)
        {
            var clip = ClipFromWav(wav);
            if (clip == null) return false;
            var audioUtil = typeof(EditorWindow).Assembly.GetType("UnityEditor.AudioUtil");
            var stop = audioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
            var play = audioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            if (play == null) return false;
            stop?.Invoke(null, null);
            play.Invoke(null, new object[] { clip, 0, false });
            return true;
        }

        // Azure sends 16-bit PCM in a RIFF/WAVE file.
        private static AudioClip ClipFromWav(byte[] wav)
        {
            if (wav == null || wav.Length < 44 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF") return null;
            int channels = 1, rate = 24000;
            for (int pos = 12; pos + 8 <= wav.Length;)
            {
                string id = Encoding.ASCII.GetString(wav, pos, 4);
                int size = BitConverter.ToInt32(wav, pos + 4);
                int body = pos + 8;
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(wav, body + 2);
                    rate = BitConverter.ToInt32(wav, body + 4);
                }
                else if (id == "data")
                {
                    size = Math.Min(size, wav.Length - body);
                    var samples = new float[size / 2];
                    for (int i = 0; i < samples.Length; i++)
                        samples[i] = BitConverter.ToInt16(wav, body + i * 2) / 32768f;
                    var clip = AudioClip.Create("Voice test", samples.Length / channels, channels, rate, false);
                    clip.SetData(samples, 0);
                    return clip;
                }
                pos = body + size + (size & 1);
            }
            return null;
        }
    }
}
