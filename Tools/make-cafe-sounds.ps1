# Synthesises placeholder sound effects for Cat Cafe Waves into Assets/Resources/CafeSounds:
#   ninja_sting.wav  two taiko drum hits and a sword "shing" (played before the ninja's callouts)
#   coin.wav         a bright coin clink (each coin landing in the tip jar)
# Swap them for real sounds later (same names), no code changes needed.
#   powershell -File Tools/make-cafe-sounds.ps1

$folder = Join-Path $PSScriptRoot '..\Assets\Resources\CafeSounds'
New-Item -ItemType Directory -Force $folder | Out-Null

Add-Type -TypeDefinition @'
using System;
using System.IO;

public static class CafeSynth
{
    const int Rate = 44100;
    static readonly Random random = new Random(7);

    static float[] Buffer(double seconds) { return new float[(int)(seconds * Rate)]; }

    // A big drum: a low tone that drops in pitch, a thump of noise, and a quick decay
    static void Taiko(float[] buffer, double start, double loudness)
    {
        int first = (int)(start * Rate);
        double phase = 0, phase2 = 0, low = 0;
        for (int i = first; i < buffer.Length; i++)
        {
            double t = (i - first) / (double)Rate;
            if (t > 1.2) break;
            double pitch = 58 + 50 * Math.Exp(-t / 0.05);
            phase += 2 * Math.PI * pitch / Rate;
            phase2 += 2 * Math.PI * pitch * 1.52 / Rate;
            double body = Math.Sin(phase) * Math.Exp(-t / 0.32) + 0.35 * Math.Sin(phase2) * Math.Exp(-t / 0.12);
            double noise = random.NextDouble() * 2 - 1;
            low += (noise - low) * 0.08;   // muffled skin slap
            double slap = low * 2.2 * Math.Exp(-t / 0.025);
            buffer[i] += (float)(loudness * (body + slap));
        }
    }

    // A blade being drawn: a rising swish into a bright metallic ring
    static void Shing(float[] buffer, double start, double loudness)
    {
        int first = (int)(start * Rate);
        double[] partials = { 2093, 2960, 3729, 4699, 5920, 7459 };
        double[] decays = { 0.9, 0.7, 0.6, 0.45, 0.35, 0.25 };
        double[] phases = new double[partials.Length];
        double band = 0, prev = 0;
        for (int i = first; i < buffer.Length; i++)
        {
            double t = (i - first) / (double)Rate;
            double sample = 0;

            // Swish: filtered noise sweeping up over 0.12 s, just before the ring
            if (t < 0.14)
            {
                double noise = random.NextDouble() * 2 - 1;
                double amount = 0.05 + 0.6 * (t / 0.14);
                band += (noise - band) * amount;
                double high = band - prev;
                prev = band;
                sample += high * 0.9 * Math.Sin(Math.PI * t / 0.14);
            }

            // Ring: starts as the swish ends, with a slight shimmer
            double r = t - 0.1;
            if (r > 0)
            {
                double attack = Math.Min(1, r / 0.004);
                for (int p = 0; p < partials.Length; p++)
                {
                    double shimmer = 1 + 0.002 * Math.Sin(2 * Math.PI * 5 * r + p);
                    phases[p] += 2 * Math.PI * partials[p] * shimmer / Rate;
                    sample += attack * Math.Sin(phases[p]) * Math.Exp(-r / decays[p]) * (0.32 - p * 0.035);
                }
            }
            buffer[i] += (float)(loudness * sample);
        }
    }

    // A coin landing: two bright pings close together
    static void Clink(float[] buffer, double start, double pitch, double loudness)
    {
        int first = (int)(start * Rate);
        double[] partials = { 2637, 3951, 5274, 6820 };
        double[] decays = { 0.16, 0.12, 0.08, 0.05 };
        double[] phases = new double[partials.Length];
        for (int i = first; i < buffer.Length; i++)
        {
            double t = (i - first) / (double)Rate;
            if (t > 0.5) break;
            double attack = Math.Min(1, t / 0.002);
            double sample = 0;
            for (int p = 0; p < partials.Length; p++)
            {
                phases[p] += 2 * Math.PI * partials[p] * pitch / Rate;
                sample += Math.Sin(phases[p]) * Math.Exp(-t / decays[p]) / (p + 1.5);
            }
            buffer[i] += (float)(loudness * attack * sample);
        }
    }

    public static void NinjaSting(string path)
    {
        float[] buffer = Buffer(1.8);
        Taiko(buffer, 0.0, 0.9);
        Taiko(buffer, 0.26, 1.0);
        Shing(buffer, 0.5, 0.7);
        Save(path, buffer);
    }

    public static void Coin(string path)
    {
        float[] buffer = Buffer(0.45);
        Clink(buffer, 0.0, 1.0, 0.9);
        Clink(buffer, 0.045, 1.07, 0.6);
        Save(path, buffer);
    }

    // Normalises to just under full volume, fades the very end, writes 16-bit mono
    static void Save(string path, float[] buffer)
    {
        float peak = 0.0001f;
        foreach (float s in buffer) peak = Math.Max(peak, Math.Abs(s));
        float gain = 0.89f / peak;
        int fade = Math.Min(buffer.Length, Rate / 50);
        for (int i = 0; i < fade; i++) buffer[buffer.Length - 1 - i] *= i / (float)fade;

        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            int bytes = buffer.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' }); w.Write(16);
            w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
            foreach (float s in buffer) w.Write((short)Math.Max(-32767, Math.Min(32767, s * gain * 32767)));
        }
    }
}
'@

[CafeSynth]::NinjaSting((Join-Path $folder 'ninja_sting.wav'))
[CafeSynth]::Coin((Join-Path $folder 'coin.wav'))
Get-ChildItem $folder -Filter *.wav | ForEach-Object { "made $($_.Name) ($([math]::Round($_.Length / 1024)) KB)" }
