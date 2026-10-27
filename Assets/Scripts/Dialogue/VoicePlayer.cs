using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ThaiMystery
{
    // Plays recorded or generated Thai voice clips when they exist, and falls back to
    // little "voice blips" while text appears, like 7 Days to End with You and Undertale.
    //
    // Clips are looked up by name in Resources/Audio:
    //   Lines/<dialogueId>_<lineIndex>   one clip per Thai line (index counts from 0)
    //   Words/<WordKey(thai)>            one clip per word in Mali's voice
    //   Words/Own/<WordKey(thai)>        the same word in the protagonist's own voice
    // Thai Mystery > Generate Voices creates them with text-to-speech.
    //
    // When the protagonist recalls a word (reading a label, clicking a word) it plays as he
    // remembers it: Mali's voice through a muffled, echoing "memory" filter while it is new,
    // clearing as he practises, and his own voice once the word is learned.
    public class VoicePlayer : MonoBehaviour
    {
        private AudioSource voice;
        private AudioSource blips;
        private AudioSource memory;
        private AudioLowPassFilter memoryLowPass;
        private AudioReverbFilter memoryReverb;
        private AudioClip blip;
        private float speakerPitch = 1f;
        private Coroutine recall;
        private List<string> lastRecall;

        public bool IsSpeaking => (voice != null && voice.isPlaying) || (memory != null && memory.isPlaying) || recall != null;

        private void Awake()
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            blips = gameObject.AddComponent<AudioSource>();
            blips.playOnAwake = false;
            blips.volume = 0.22f;
            blip = MakeBlip();

            // Filters affect every source on their GameObject, so the memory voice gets its own.
            var memoryObject = new GameObject("Memory Voice");
            memoryObject.transform.SetParent(transform, false);
            memory = memoryObject.AddComponent<AudioSource>();
            memory.playOnAwake = false;
            memoryLowPass = memoryObject.AddComponent<AudioLowPassFilter>();
            memoryReverb = memoryObject.AddComponent<AudioReverbFilter>();
            memoryReverb.reverbPreset = AudioReverbPreset.User;

            if (FindAnyObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
        }

        public void SetSpeaker(string speaker) => speakerPitch = PitchFor(speaker);

        // Returns true when a recorded clip exists for this line.
        public bool PlayLine(string dialogueId, int lineIndex)
        {
            Stop();
            var clip = Resources.Load<AudioClip>($"Audio/Lines/{dialogueId}_{lineIndex}");
            if (clip == null) return false;
            voice.clip = clip;
            voice.Play();
            return true;
        }

        // Plays a word the way the protagonist remembers it. Returns false if he can't
        // (he has never heard it, or there is no clip for it).
        public bool PlayWord(string thai)
        {
            if (!CanRecall(thai)) return false;
            Stop();
            lastRecall = new List<string> { thai };
            PlayRecalled(thai, Game.Words.StageOf(thai), Game.Words.Fog(thai));
            return true;
        }

        public static bool CanRecall(string thai) =>
            Game.Words.IsHeard(thai) && (MaliClip(thai) != null || OwnClip(thai) != null);

        // Reads words out one after another as remembered, e.g. a label on an object.
        // Returns false when none of them can be recalled.
        public bool PlayRecall(List<string> words)
        {
            Stop();
            var playable = words.FindAll(CanRecall);
            if (playable.Count == 0) return false;
            lastRecall = playable;
            // Read the stages now: the caller counts this recall as practice straight after.
            var stages = playable.ConvertAll(Game.Words.StageOf);
            var fogs = playable.ConvertAll(Game.Words.Fog);
            recall = StartCoroutine(RecallRoutine(playable, stages, fogs));
            return true;
        }

        private IEnumerator RecallRoutine(List<string> words, List<WordStage> stages, List<float> fogs)
        {
            for (int i = 0; i < words.Count; i++)
            {
                var clip = PlayRecalled(words[i], stages[i], fogs[i]);
                yield return new WaitForSeconds(clip.length + 0.15f);
            }
            recall = null;
        }

        private AudioClip PlayRecalled(string thai, WordStage stage, float fog)
        {
            var own = OwnClip(thai);
            if (stage == WordStage.Learned && own != null)
            {
                voice.clip = own;
                voice.Play();
                return own;
            }
            var clip = MaliClip(thai) ?? own;
            SetFog(fog);
            memory.clip = clip;
            memory.Play();
            return clip;
        }

        // 1 = barely there: dull, far away and echoing. 0 = clear.
        private void SetFog(float fog)
        {
            memory.volume = Mathf.Lerp(1f, 0.55f, fog);
            memory.pitch = Mathf.Lerp(1f, 0.96f, fog);
            memoryLowPass.cutoffFrequency = Mathf.Lerp(6000f, 650f, fog);
            memoryLowPass.lowpassResonanceQ = 1f;
            // Reverb levels are in millibels: 0 is full, -10000 is silent.
            memoryReverb.dryLevel = Mathf.Lerp(0f, -900f, fog);
            memoryReverb.room = Mathf.Lerp(-10000f, -300f, fog);
            memoryReverb.roomHF = Mathf.Lerp(-10000f, -1600f, fog);
            memoryReverb.decayTime = Mathf.Lerp(0.6f, 2.8f, fog);
            memoryReverb.reflectionsLevel = Mathf.Lerp(-10000f, -1000f, fog);
            memoryReverb.reverbLevel = Mathf.Lerp(-10000f, 200f, fog);
            memoryReverb.reverbDelay = 0.04f;
        }

        private static AudioClip MaliClip(string thai) => Resources.Load<AudioClip>("Audio/Words/" + WordKey(thai));

        private static AudioClip OwnClip(string thai) => Resources.Load<AudioClip>("Audio/Words/Own/" + WordKey(thai));

        public void Replay()
        {
            if (lastRecall != null)
            {
                var words = lastRecall;
                PlayRecall(words);
            }
            else if (voice.clip != null)
            {
                voice.Stop();
                voice.Play();
            }
        }

        public void Stop()
        {
            if (recall != null) StopCoroutine(recall);
            recall = null;
            lastRecall = null;
            voice.Stop();
            voice.clip = null;
            memory.Stop();
        }

        public void Blip(bool soft = false)
        {
            blips.pitch = speakerPitch * Random.Range(0.94f, 1.06f) * (soft ? 0.8f : 1f);
            blips.PlayOneShot(blip, soft ? 0.6f : 1f);
        }

        // File-system safe name for a Thai word: its code points in hex, e.g. "w_0e04_0e38_0e13".
        // Tools/generate_voices.py uses the same rule.
        public static string WordKey(string thai)
        {
            var sb = new StringBuilder("w");
            foreach (char ch in thai) sb.Append('_').Append(((int)ch).ToString("x4"));
            return sb.ToString();
        }

        // Each speaker gets a stable blip pitch; narration and thoughts sit lower.
        private static float PitchFor(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return 0.8f;
            int h = 0;
            foreach (char c in speaker) h = h * 31 + c;
            return 1.05f + (Mathf.Abs(h) % 30) / 100f;
        }

        private static AudioClip MakeBlip()
        {
            const int rate = 22050;
            int samples = rate / 22;
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Sin(Mathf.PI * i / samples);
                // A soft square-ish tone reads as "speech" better than a pure sine.
                float wave = Mathf.Sin(2f * Mathf.PI * 520f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 1040f * t);
                data[i] = 0.5f * env * env * wave;
            }
            var clip = AudioClip.Create("voice_blip", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
