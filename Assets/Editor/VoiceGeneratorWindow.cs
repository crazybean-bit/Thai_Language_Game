using System;
using System.Collections.Generic;
using System.IO;
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
    public class VoiceGeneratorWindow : EditorWindow
    {
        private const string Prefs = "ThaiMystery.Voices.";
        private const string AudioRoot = "Assets/Resources/Audio";
        private const double MinSecondsBetweenRequests = 0.4;

        [Serializable]
        private class SpeakerVoice
        {
            public string speaker;
            public string voice;
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
            public string voice;
            public string rate;
        }

        private string apiKey = "";
        private string region = "southeastasia";
        private string narrationVoice = "th-TH-NiwatNeural";
        private string wordVoice = "th-TH-PremwadeeNeural";
        private string defaultCharacterVoice = "th-TH-PremwadeeNeural";
        private bool slowWords = true;
        private bool overwrite;
        private SpeakerVoiceList speakers = new SpeakerVoiceList();

        private readonly Queue<Job> queue = new Queue<Job>();
        private UnityWebRequest request;
        private Job running;
        private int total;
        private int finished;
        private double nextRequestTime;
        private string status = "";
        private Vector2 scroll;

        [MenuItem("Thai Mystery/Generate Voices...")]
        public static void Open() => GetWindow<VoiceGeneratorWindow>("Thai Voices");

        private void OnEnable()
        {
            apiKey = EditorPrefs.GetString(Prefs + "Key", "");
            region = EditorPrefs.GetString(Prefs + "Region", region);
            narrationVoice = EditorPrefs.GetString(Prefs + "Narration", narrationVoice);
            wordVoice = EditorPrefs.GetString(Prefs + "Word", wordVoice);
            defaultCharacterVoice = EditorPrefs.GetString(Prefs + "Character", defaultCharacterVoice);
            slowWords = EditorPrefs.GetBool(Prefs + "SlowWords", true);
            var saved = EditorPrefs.GetString(Prefs + "Speakers", "");
            if (!string.IsNullOrEmpty(saved)) speakers = JsonUtility.FromJson<SpeakerVoiceList>(saved);
            if (speakers.items.Count == 0)
            {
                speakers.items.Add(new SpeakerVoice { speaker = "มะลิ", voice = "th-TH-PremwadeeNeural" });
                speakers.items.Add(new SpeakerVoice { speaker = "???", voice = "th-TH-PremwadeeNeural" });
            }
        }

        private void OnDisable() => StopAll("Stopped.");

        private void SavePrefs()
        {
            EditorPrefs.SetString(Prefs + "Key", apiKey);
            EditorPrefs.SetString(Prefs + "Region", region);
            EditorPrefs.SetString(Prefs + "Narration", narrationVoice);
            EditorPrefs.SetString(Prefs + "Word", wordVoice);
            EditorPrefs.SetString(Prefs + "Character", defaultCharacterVoice);
            EditorPrefs.SetBool(Prefs + "SlowWords", slowWords);
            EditorPrefs.SetString(Prefs + "Speakers", JsonUtility.ToJson(speakers));
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "Creates Thai voice clips for every Thai line in dialogue.json and every word in words.json, " +
                "using Azure Speech. Paste KEY 1 and the region from your Speech resource's 'Keys and Endpoint' page. " +
                "The key is stored only on this computer.", MessageType.Info);

            using (new EditorGUI.DisabledScope(IsRunning))
            {
                EditorGUILayout.LabelField("Azure Speech", EditorStyles.boldLabel);
                apiKey = EditorGUILayout.PasswordField("Key", apiKey);
                region = EditorGUILayout.TextField("Region", region);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Voices", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Thai voices: th-TH-PremwadeeNeural (female), th-TH-AcharaNeural (female), th-TH-NiwatNeural (male)",
                    EditorStyles.miniLabel);
                wordVoice = EditorGUILayout.TextField("Single words", wordVoice);
                narrationVoice = EditorGUILayout.TextField("Narration (no speaker)", narrationVoice);
                defaultCharacterVoice = EditorGUILayout.TextField("Other characters", defaultCharacterVoice);
                for (int i = 0; i < speakers.items.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    speakers.items[i].speaker = EditorGUILayout.TextField(speakers.items[i].speaker, GUILayout.Width(120));
                    speakers.items[i].voice = EditorGUILayout.TextField(speakers.items[i].voice);
                    if (GUILayout.Button("x", GUILayout.Width(22))) { speakers.items.RemoveAt(i); break; }
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button("Add character voice", GUILayout.Width(160)))
                    speakers.items.Add(new SpeakerVoice { speaker = "", voice = defaultCharacterVoice });

                EditorGUILayout.Space();
                slowWords = EditorGUILayout.Toggle(new GUIContent("Slower single words", "Reads single words 20% slower, easier for learners"), slowWords);
                overwrite = EditorGUILayout.Toggle(new GUIContent("Redo existing clips", "Regenerate clips that already exist, e.g. after editing a line"), overwrite);
            }

            EditorGUILayout.Space();
            if (!IsRunning)
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(region)))
                {
                    if (GUILayout.Button("Generate voices", GUILayout.Height(30))) Generate();
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

        private bool IsRunning => running != null || queue.Count > 0;

        private void Generate()
        {
            SavePrefs();
            queue.Clear();
            var jobs = CollectJobs();
            foreach (var job in jobs)
                if (overwrite || !File.Exists(job.path)) queue.Enqueue(job);

            total = queue.Count;
            finished = 0;
            if (total == 0)
            {
                status = $"All {jobs.Count} clips already exist. Tick 'Redo existing clips' to make them again.";
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
                    jobs.Add(new Job
                    {
                        path = $"{AudioRoot}/Lines/{d.id}_{i}.wav",
                        // The game spaces words out for learners; spoken Thai runs them together.
                        text = line.text.Replace(" ", ""),
                        voice = VoiceFor(line.speaker),
                        rate = "0%"
                    });
                }
            }
            foreach (var w in words.words)
            {
                jobs.Add(new Job
                {
                    path = $"{AudioRoot}/Words/{VoicePlayer.WordKey(w.thai.Trim())}.wav",
                    text = w.thai.Trim(),
                    voice = wordVoice,
                    rate = slowWords ? "-20%" : "0%"
                });
            }
            return jobs;
        }

        private string VoiceFor(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return narrationVoice;
            foreach (var s in speakers.items)
                if (s.speaker == speaker && !string.IsNullOrEmpty(s.voice)) return s.voice;
            return defaultCharacterVoice;
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
            request = BuildRequest(running);
            request.SendWebRequest();
        }

        private UnityWebRequest BuildRequest(Job job)
        {
            string ssml =
                "<speak version=\"1.0\" xml:lang=\"th-TH\" xmlns=\"http://www.w3.org/2001/10/synthesis\">" +
                $"<voice name=\"{SecurityElement.Escape(job.voice)}\"><prosody rate=\"{job.rate}\">" +
                SecurityElement.Escape(job.text) + "</prosody></voice></speak>";

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
                string hint = req.responseCode == 401 ? " Check the key and region match your Speech resource."
                    : req.responseCode == 400 ? " Check the voice names are spelled correctly." : "";
                StopAll($"Failed on {running?.path}: {req.responseCode} {req.error}.{hint}");
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
    }
}
