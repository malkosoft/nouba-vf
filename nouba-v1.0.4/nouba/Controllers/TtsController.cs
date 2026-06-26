using Microsoft.AspNetCore.Mvc;
using Nouba.Helpers;
using Nouba.Services;
using System.Text;

namespace Nouba.Controllers;

[Route("Tts")]
public class TtsController : Controller
{
    private readonly PiperTtsService _tts;

    public TtsController(PiperTtsService tts)
    {
        _tts = tts;
    }

    [HttpGet("Speak")]
    public async Task<IActionResult> Speak(string text, string lang = "fr", string? gender = null, double? lengthScale = null, bool strip = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return BadRequest("text required");

        var safeLang = (lang ?? "fr").Trim().ToLowerInvariant();
        if (safeLang != "fr" && safeLang != "en" && safeLang != "ar")
            return StatusCode(204);

        var safeGender = (gender ?? "").Trim().ToLowerInvariant();
        if (safeGender != "male" && safeGender != "female") safeGender = string.Empty;

        // Outil de réglage (/tts-tuning.html) : permet de comparer à l'oreille la
        // version « tashkeel manuel » et la version « sans tashkeel » (laissée à
        // libtashkeel). N'affecte que l'arabe ; ignoré pour fr/en.
        if (strip && safeLang == "ar")
            text = AnnouncementTextBuilder.StripArabicDiacritics(text);

        // Produit terrain / Smart TV : ne jamais couper Piper uniquement parce que
        // le genre demandé n'existe pas. Sur TV et smartphone, le fallback Web Speech
        // est souvent absent. Le service choisit donc le meilleur modèle disponible
        // (FR/EN fournis, AR féminin si présent, sinon AR masculin).
        byte[]? wav = null;
        try
        {
            wav = await _tts.SynthesizeAsync(text, safeLang, safeGender, lengthScale, HttpContext.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            // Client parti : pas une erreur produit.
            return StatusCode(204);
        }

        if (wav == null || wav.Length < 44)
        {
            // On indique pourquoi, pour le diagnostic côté navigateur (panneau TV).
            Response.Headers["X-Piper"] = _tts.IsLanguageAvailable(safeLang, safeGender)
                ? "synth-failed"
                : "model-missing";
            return StatusCode(204);
        }

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        Response.Headers["X-Piper"] = "ok";

        return File(wav, "audio/wav");
    }

    [HttpGet("UnlockBeep")]
    public IActionResult UnlockBeep()
    {
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        return File(CreateUnlockBeepWav(), "audio/wav");
    }

    [HttpGet("Status")]
    public IActionResult Status()
    {
        Response.Headers["Cache-Control"] = "no-store";
        return Json(_tts.GetStatus());
    }

    private static byte[] CreateUnlockBeepWav()
    {
        const int sampleRate = 44100;
        const int channels = 1;
        const int bitsPerSample = 16;
        const int durationMs = 240;
        const double frequency = 880.0;
        const double gain = 0.12;

        var sampleCount = sampleRate * durationMs / 1000;
        var blockAlign = channels * bitsPerSample / 8;
        var dataSize = sampleCount * blockAlign;

        using var ms = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        var fadeSamples = Math.Max(1, sampleRate * 24 / 1000);
        for (var i = 0; i < sampleCount; i++)
        {
            var fadeIn = Math.Min(1.0, i / (double)fadeSamples);
            var fadeOut = Math.Min(1.0, (sampleCount - i - 1) / (double)fadeSamples);
            var envelope = Math.Min(fadeIn, fadeOut);
            var value = Math.Sin(2.0 * Math.PI * frequency * i / sampleRate) * gain * envelope;
            writer.Write((short)Math.Round(value * short.MaxValue));
        }

        writer.Flush();
        return ms.ToArray();
    }
}
