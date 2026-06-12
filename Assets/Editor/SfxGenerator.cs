#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace SuperSausageBoy.EditorTools
{
    /// <summary>
    /// Synthesizes ORIGINAL retro 8-bit style sound effects procedurally and
    /// writes them as 16-bit mono WAV files into Assets/Audio/SFX. Everything is
    /// generated from math (square/triangle/noise oscillators + envelopes) so the
    /// SFX are 100% original with a classic arcade feel — no sampled assets.
    ///
    /// Run via menu: SuperSausageBoy/Generate SFX, or headless:
    ///   -executeMethod SuperSausageBoy.EditorTools.SfxGenerator.Generate
    /// </summary>
    public static class SfxGenerator
    {
        const int SampleRate = 44100;
        const string OutDir = "Assets/Audio/SFX";

        [MenuItem("SuperSausageBoy/Generate SFX")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutDir);

            Save("jump", BuildJump());
            Save("land", BuildLand());
            Save("death", BuildDeath());
            Save("wallslide", BuildWallSlide());
            Save("goal", BuildGoal());

            AssetDatabase.Refresh();
            Debug.Log("[SSB] SFX generated in " + OutDir);
        }

        // ---------------- sound designs ----------------

        // Jump: quick upward square-wave "blip" (rising pitch), short decay.
        static float[] BuildJump()
        {
            float dur = 0.16f;
            int n = (int)(dur * SampleRate);
            var buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float freq = Mathf.Lerp(360f, 760f, t / dur);
                float sq = Square(freq, t, 0.5f);
                float env = Env(i, n, 0.005f, 0.04f);
                buf[i] = sq * env * 0.5f;
            }
            return buf;
        }

        // Land: short low thud — triangle + a touch of noise, fast decay.
        static float[] BuildLand()
        {
            float dur = 0.12f;
            int n = (int)(dur * SampleRate);
            var buf = new float[n];
            var rng = new System.Random(11);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float freq = Mathf.Lerp(180f, 90f, t / dur);
                float tri = Triangle(freq, t);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.25f;
                float env = Env(i, n, 0.002f, 0.05f);
                buf[i] = (tri * 0.7f + noise) * env * 0.6f;
            }
            return buf;
        }

        // Death: descending "splat" — falling square pitch + noise burst.
        static float[] BuildDeath()
        {
            float dur = 0.45f;
            int n = (int)(dur * SampleRate);
            var buf = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float p = t / dur;
                float freq = Mathf.Lerp(520f, 60f, p * p);
                float sq = Square(freq, t, 0.5f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float noiseEnv = Mathf.Clamp01(1f - p * 3f); // noise only at the start
                float env = Env(i, n, 0.002f, 0.25f);
                buf[i] = (sq * 0.6f + noise * 0.4f * noiseEnv) * env * 0.55f;
            }
            return buf;
        }

        // Wall-slide: looping airy noise hiss (designed to loop seamlessly).
        static float[] BuildWallSlide()
        {
            float dur = 0.5f;
            int n = (int)(dur * SampleRate);
            var buf = new float[n];
            var rng = new System.Random(23);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                // low-passed noise for a soft "shhh"
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                prev = Mathf.Lerp(prev, white, 0.15f);
                buf[i] = prev * 0.35f;
            }
            // crossfade head/tail so the loop has no click
            int fade = (int)(0.02f * SampleRate);
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                buf[i] *= k;
                buf[n - 1 - i] *= k;
            }
            return buf;
        }

        // Goal: cheerful rising arpeggio (3 square-wave notes) — victory jingle.
        static float[] BuildGoal()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f }; // C5 E5 G5 C6
            float noteDur = 0.11f;
            int per = (int)(noteDur * SampleRate);
            int n = per * notes.Length;
            var buf = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                for (int i = 0; i < per; i++)
                {
                    float t = (float)i / SampleRate;
                    float sq = Square(notes[k], t, 0.5f);
                    float env = Env(i, per, 0.005f, 0.05f);
                    buf[k * per + i] = sq * env * 0.45f;
                }
            }
            return buf;
        }

        // ---------------- oscillators / envelopes ----------------

        static float Square(float freq, float t, float duty)
        {
            float phase = (freq * t) % 1f;
            return phase < duty ? 1f : -1f;
        }

        static float Triangle(float freq, float t)
        {
            float phase = (freq * t) % 1f;
            return 4f * Mathf.Abs(phase - 0.5f) - 1f;
        }

        // Simple attack/decay (linear attack, exponential-ish decay) envelope.
        static float Env(int i, int n, float attackSec, float decaySec)
        {
            float t = (float)i / SampleRate;
            float total = (float)n / SampleRate;
            float remaining = total - t;
            float a = attackSec > 0f ? Mathf.Clamp01(t / attackSec) : 1f;
            float d = decaySec > 0f ? Mathf.Clamp01(remaining / decaySec) : 1f;
            return a * d;
        }

        // ---------------- WAV writer (16-bit PCM mono) ----------------

        static void Save(string name, float[] samples)
        {
            string path = $"{OutDir}/{name}.wav";
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                int byteRate = SampleRate * 2;       // mono, 16-bit
                int dataLen = samples.Length * 2;

                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataLen);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);                         // subchunk1 size
                bw.Write((short)1);                   // PCM
                bw.Write((short)1);                   // channels = mono
                bw.Write(SampleRate);
                bw.Write(byteRate);
                bw.Write((short)2);                   // block align
                bw.Write((short)16);                  // bits per sample
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(dataLen);

                for (int i = 0; i < samples.Length; i++)
                {
                    float v = Mathf.Clamp(samples[i], -1f, 1f);
                    bw.Write((short)(v * short.MaxValue));
                }
            }

            AssetDatabase.ImportAsset(path);
            Debug.Log("[SSB] wrote " + path + " (" + samples.Length + " samples)");
        }
    }
}
#endif
