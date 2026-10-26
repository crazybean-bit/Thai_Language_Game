using System.Text;
using UnityEngine;

namespace ThaiMystery
{
    // Plays recorded or generated Thai voice clips when they exist, and falls back to
    // little "voice blips" while text appears, like 7 Days to End with You and Undertale.
    //
    // Clips are looked up by name in Resources/Audio:
    //   Lines/<dialogueId>_<lineIndex>   one clip per Thai line (index counts from 0)
    //   Words/<WordKey(thai)>            one clip per word, played when you click it
    // Tools/generate_voices.py creates them with text-to-speech.
    public class VoicePlayer : MonoBehaviour
    {
        private AudioSource voice;
        private AudioSource blips;
        private AudioClip blip;
        private float speakerPitch = 1f;

        public bool IsSpeaking => voice != null && voice.isPlaying;

        private void Awake()
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            blips = gameObject.AddComponent<AudioSource>();
            blips.playOnAwake = false;
            blips.volume = 0.22f;
            blip = MakeBlip();

            if (FindAnyObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
        }

        public void SetSpeaker(string speaker) => speakerPitch = PitchFor(speaker);

        // Returns true when a recorded clip exists for this line.
        public bool PlayLine(string dialogueId, int lineIndex)
        {
            voice.Stop();
            var clip = Resources.Load<AudioClip>($"Audio/Lines/{dialogueId}_{lineIndex}");
            if (clip == null)
            {
                voice.clip = null;
                return false;
            }
            voice.clip = clip;
            voice.Play();
            return true;
        }

        public bool PlayWord(string thai)
        {
            var clip = Resources.Load<AudioClip>("Audio/Words/" + WordKey(thai));
            if (clip == null) return false;
            voice.Stop();
            voice.clip = clip;
            voice.Play();
            return true;
        }

        public static bool HasWordClip(string thai) =>
            Resources.Load<AudioClip>("Audio/Words/" + WordKey(thai)) != null;

        public void Replay()
        {
            if (voice.clip != null)
            {
                voice.Stop();
                voice.Play();
            }
        }

        public void Stop() => voice.Stop();

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
