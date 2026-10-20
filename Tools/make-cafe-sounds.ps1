# Synthesises placeholder sound effects for Cat Cafe Waves into Assets/Resources/CafeSounds:
#   ninja_sting.wav  two taiko drum hits and a sword "shing" (played before the ninja's callouts)
#   coin.wav         a bright coin clink (each coin landing in the tip jar)
#   windup.wav       a rising cartoon wind-up (a cat getting ready to raid)
#   boing.wav        a springy boing (a pop-up cat about to spring up)
#   order_ding.wav   a counter-bell ding (a cat has just ordered)
#   spy_sting.wav    a sneaky plucked-bass "dum-dum... da-dum" (a spy cat coming down from the ceiling)
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

    // A cartoon wind-up before a pounce: a buzzy tone sliding up with a fast wobble, cut off sharply at the top
    public static void WindUp(string path)
    {
        float[] buffer = Buffer(1.15);
        double phase = 0, low = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            double t = i / (double)Rate;
            double pitch = 220 * Math.Pow(2, t * 1.9);
            phase += 2 * Math.PI * pitch / Rate;
            double tone = Math.Sin(phase) + 0.45 * Math.Sign(Math.Sin(phase)) * 0.5 + 0.25 * Math.Sin(phase * 2);
            double noise = random.NextDouble() * 2 - 1;
            low += (noise - low) * 0.05;
            double growl = low * 1.5 * (1 - t / 1.15);
            double wobble = 0.65 + 0.35 * Math.Sin(2 * Math.PI * (10 + 8 * t) * t);
            double envelope = Math.Min(1, t / 0.08) * (0.55 + 0.45 * t / 1.15) * Math.Min(1, (1.15 - t) / 0.03);
            buffer[i] = (float)((tone * wobble + growl) * envelope);
        }
        Save(path, buffer);
    }

    // A bright counter-bell ding: a cat has ordered
    public static void Ding(string path)
    {
        float[] buffer = Buffer(0.9);
        double[] partials = { 1318.5, 1975.5, 2637, 3951 };
        double[] decays = { 0.45, 0.35, 0.22, 0.12 };
        double[] loud = { 1.0, 0.55, 0.3, 0.15 };
        double[] phases = new double[partials.Length];
        for (int i = 0; i < buffer.Length; i++)
        {
            double t = i / (double)Rate;
            double attack = Math.Min(1, t / 0.003);
            double sample = 0;
            for (int p = 0; p < partials.Length; p++)
            {
                double shimmer = 1 + 0.0015 * Math.Sin(2 * Math.PI * 6 * t + p);
                phases[p] += 2 * Math.PI * partials[p] * shimmer / Rate;
                sample += loud[p] * Math.Sin(phases[p]) * Math.Exp(-t / decays[p]);
            }
            buffer[i] = (float)(attack * sample);
        }
        Save(path, buffer);
    }

    // A springy cartoon boing: a quick swoop up with a wobble that rings out
    public static void Boing(string path)
    {
        float[] buffer = Buffer(0.5);
        double phase = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            double t = i / (double)Rate;
            double swoop = 180 + 520 * (1 - Math.Exp(-t / 0.05));
            double wobble = 1 + 0.12 * Math.Sin(2 * Math.PI * 22 * t) * Math.Exp(-t / 0.2);
            phase += 2 * Math.PI * swoop * wobble / Rate;
            double tone = Math.Sin(phase) + 0.3 * Math.Sin(phase * 2) + 0.15 * Math.Sin(phase * 3);
            double envelope = Math.Min(1, t / 0.005) * Math.Exp(-t / 0.16);
            buffer[i] = (float)(tone * envelope);
        }
        Save(path, buffer);
    }

    public static void NinjaSting(string path)
    {
        float[] buffer = Buffer(1.8);
        Taiko(buffer, 0.0, 0.9);
        Taiko(buffer, 0.26, 1.0);
        Shing(buffer, 0.5, 0.7);
        Save(path, buffer);
    }

    // A plucked double-bass note (Karplus-Strong: a burst of noise ringing in a short delay line)
    static void Pluck(float[] buffer, double start, double frequency, double loudness, double ring)
    {
        int first = (int)(start * Rate);
        int length = Math.Max(2, (int)(Rate / frequency));
        double[] line = new double[length];
        for (int i = 0; i < length; i++) line[i] = random.NextDouble() * 2 - 1;
        int at = 0;
        for (int i = first; i < buffer.Length; i++)
        {
            double t = (i - first) / (double)Rate;
            double next = line[(at + 1) % length];
            double sample = line[at];
            line[at] = 0.5 * (sample + next) * 0.996;
            at = (at + 1) % length;
            buffer[i] += (float)(sample * loudness * Math.Exp(-t / ring));
        }
    }

    // A soft brush tick on a snare
    static void Brush(float[] buffer, double start, double loudness)
    {
        int first = (int)(start * Rate);
        double low = 0;
        for (int i = first; i < buffer.Length && i < first + Rate / 6; i++)
        {
            double t = (i - first) / (double)Rate;
            double noise = random.NextDouble() * 2 - 1;
            low += 0.35 * (noise - low);
            buffer[i] += (float)((noise - low) * loudness * Math.Exp(-t / 0.035));
        }
    }

    // A sneaky spy motif: dum-dum... da-dum on a low plucked bass, with brush ticks
    public static void SpySting(string path)
    {
        float[] buffer = Buffer(2.2);
        double e = 82.41, g = 98.0, a = 110.0;
        Pluck(buffer, 0.00, e, 1.0, 0.35);
        Pluck(buffer, 0.26, e, 0.9, 0.35);
        Brush(buffer, 0.13, 0.25);
        Pluck(buffer, 0.78, g, 0.95, 0.3);
        Pluck(buffer, 1.04, a, 1.0, 0.5);
        Brush(buffer, 0.91, 0.25);
        Brush(buffer, 1.17, 0.3);
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
[CafeSynth]::WindUp((Join-Path $folder 'windup.wav'))
[CafeSynth]::Boing((Join-Path $folder 'boing.wav'))
[CafeSynth]::Ding((Join-Path $folder 'order_ding.wav'))
[CafeSynth]::SpySting((Join-Path $folder 'spy_sting.wav'))
Get-ChildItem $folder -Filter *.wav | ForEach-Object { "made $($_.Name) ($([math]::Round($_.Length / 1024)) KB)" }
