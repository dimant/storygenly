using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StoryGenly.AI;

public record ChunkRecord(string filename, int index, string code_chunk, string hash);

public static class TextChunker
{
    // Toggle stripping boilerplate
    public static IEnumerable<ChunkRecord> ChunkDirectory(
        string dir,
        int maxChars = 4000,
        int overlapChars = 600,
        bool stripGutenberg = true)
    {
        foreach (var path in Directory.EnumerateFiles(dir, "*.txt", SearchOption.AllDirectories).OrderBy(p => p))
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            if (stripGutenberg) text = ExtractBody(text);
            text = Normalize(text);
            if (string.IsNullOrWhiteSpace(text)) continue;

            int idx = 0;
            foreach (var chunk in BuildCharChunks(text, maxChars, overlapChars))
                yield return new ChunkRecord(Path.GetFileName(path), idx++, chunk, GetChunkHash(chunk));
        }
    }

    private static string GetChunkHash(string chunk)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(chunk);
        var hashBytes = sha.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes); // or use Base64
    }

    // If you prefer a strict UTF-8 byte budget instead of chars, use this:
    public static IEnumerable<ChunkRecord> ChunkDirectoryByBytes(
        string dir,
        int maxBytes = 12000,
        int overlapBytes = 2000,
        bool stripGutenberg = true)
    {
        foreach (var path in Directory.EnumerateFiles(dir, "*.txt", SearchOption.AllDirectories).OrderBy(p => p))
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            if (stripGutenberg) text = ExtractBody(text);
            text = Normalize(text);
            if (string.IsNullOrWhiteSpace(text)) continue;

            int idx = 0;
            foreach (var chunk in BuildUtf8ByteChunks(text, maxBytes, overlapBytes))
                yield return new ChunkRecord(Path.GetFileName(path), idx++, chunk, GetChunkHash(chunk));
        }
    }

    // ---------------- internals ----------------

    static readonly Regex StartR = new(@"\*{3}\s*START OF.*?\*{3}", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    static readonly Regex EndR   = new(@"\*{3}\s*END OF.*?\*{3}",   RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    static readonly Regex HeaderFooterR = new(@"Project Gutenberg.*?License|End of the Project Gutenberg", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex MultiSpace = new(@"\s{2,}", RegexOptions.Compiled);
    static readonly Regex Paragraphs = new(@"\r?\n\r?\n", RegexOptions.Compiled);

    static string ExtractBody(string s)
    {
        var start = StartR.Match(s); var end = EndR.Match(s);
        if (start.Success && end.Success && end.Index > start.Index)
            s = s.Substring(start.Index + start.Length, end.Index - (start.Index + start.Length));
        s = HeaderFooterR.Split(s)[0];
        return s;
    }

    static string Normalize(string s)
    {
        s = s.Replace("\r\n", "\n").Replace("\t", " ");
        s = MultiSpace.Replace(s, " ");
        s = Regex.Replace(s, @"(\s*\n\s*){2,}", "\n\n"); // preserve paragraph breaks
        return s.Trim();
    }

    static IEnumerable<string> BuildCharChunks(string text, int maxChars, int overlapChars)
    {
        // First split into paragraphs; then add paragraphs to a buffer until near the budget.
        var paras = Paragraphs.Split(text).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        var buf = new StringBuilder();
        foreach (var p in paras)
        {
            if (p.Length > maxChars)
            {
                foreach (var s in SplitParagraphRespectingSentences(p, maxChars))
                    foreach (var c in AddWithFlush(cand: s, buf, maxChars, overlapChars))
                        yield return c;
                continue;
            }
            foreach (var c in AddWithFlush(cand: p, buf, maxChars, overlapChars))
                yield return c;
        }
        if (buf.Length > 0) yield return buf.ToString();
    }

    static IEnumerable<string> SplitParagraphRespectingSentences(string paragraph, int maxChars)
    {
        var sents = Regex.Split(paragraph, @"(?<=[\.!\?])\s+").Where(s => s.Length > 0);
        var cur = new StringBuilder();
        foreach (var s in sents)
        {
            if (s.Length > maxChars) // pathological single sentence: hard split
            {
                var start = 0;
                while (start < s.Length)
                {
                    var take = Math.Min(maxChars, s.Length - start);
                    if (cur.Length > 0) { yield return cur.ToString(); cur.Clear(); }
                    yield return s.Substring(start, take);
                    start += take;
                }
                continue;
            }
            if (cur.Length + (cur.Length > 0 ? 2 : 0) + s.Length <= maxChars)
            {
                if (cur.Length > 0) cur.Append("\n\n");
                cur.Append(s);
            }
            else
            {
                if (cur.Length > 0) { yield return cur.ToString(); cur.Clear(); }
                cur.Append(s);
            }
        }
        if (cur.Length > 0) { yield return cur.ToString(); }
    }

    static IEnumerable<string> AddWithFlush(string cand, StringBuilder buf, int maxChars, int overlapChars)
    {
        // Try to append candidate paragraph; if it would exceed, flush buffer with overlap behavior.
        if (buf.Length == 0)
        {
            buf.Append(cand);
            yield break;
        }
        if (buf.Length + 2 + cand.Length <= maxChars)
        {
            buf.Append("\n\n").Append(cand);
            yield break;
        }
        // Flush current buffer
        var chunk = buf.ToString();
        yield return chunk;

        // Build overlap seed for next buffer
        if (overlapChars > 0)
        {
            var tailLen = Math.Min(overlapChars, chunk.Length);
            var overlap = chunk.Substring(chunk.Length - tailLen, tailLen);
            buf.Clear();
            buf.Append(overlap);
            // Ensure paragraph boundary
            if (!overlap.EndsWith("\n\n")) buf.Append("\n\n");
        }
        else buf.Clear();

        // Add candidate (may still be too big; caller handles long paragraphs)
        if (cand.Length > 0)
        {
            if (buf.Length > 0 && !buf.ToString().EndsWith("\n\n")) buf.Append("\n\n");
            buf.Append(cand);
            // If we still overflow (tiny maxChars), emit immediately
            if (buf.Length > maxChars)
            {
                var c = buf.ToString();
                yield return c.Substring(0, maxChars);
                buf.Clear();
                var rem = c.Substring(maxChars);
                buf.Append(rem);
            }
        }
    }

    static IEnumerable<string> BuildUtf8ByteChunks(string text, int maxBytes, int overlapBytes)
    {
        // Reuse char-chunker logic but enforce UTF-8 byte budgets.
        var enc = Encoding.UTF8;
        var paras = Paragraphs.Split(text).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        var buf = new StringBuilder();

        foreach (var p in paras)
        {
            if (enc.GetByteCount(p) > maxBytes)
            {
                foreach (var s in SplitParagraphRespectingSentences(p, int.MaxValue))
                {
                    foreach (var c in AddWithFlushBytes(s, buf, maxBytes, overlapBytes, enc))
                        yield return c;
                }
                continue;
            }
            foreach (var c in AddWithFlushBytes(p, buf, maxBytes, overlapBytes, enc))
                yield return c;
        }
        if (buf.Length > 0) yield return buf.ToString();
    }

    static IEnumerable<string> AddWithFlushBytes(string cand, StringBuilder buf, int maxBytes, int overlapBytes, Encoding enc)
    {
        string WithSep(StringBuilder b, string s) => b.Length == 0 ? s : "\n\n" + s;

        if (buf.Length == 0)
        {
            if (enc.GetByteCount(cand) <= maxBytes) { buf.Append(cand); yield break; }
        }
        var tryCombined = buf.ToString() + WithSep(buf, cand);
        if (enc.GetByteCount(tryCombined) <= maxBytes)
        {
            if (buf.Length > 0) buf.Append("\n\n");
            buf.Append(cand);
            yield break;
        }

        // Flush current buffer
        var chunk = buf.ToString();
        if (!string.IsNullOrEmpty(chunk)) yield return chunk;

        // Build byte-overlap
        if (overlapBytes > 0 && !string.IsNullOrEmpty(chunk))
        {
            // Take overlap by bytes without breaking UTF-8
            var bytes = enc.GetBytes(chunk);
            var tailLen = Math.Min(overlapBytes, bytes.Length);
            var tail = new ReadOnlySpan<byte>(bytes, bytes.Length - tailLen, tailLen);

            // Back up to the start of a UTF-8 codepoint
            int start = 0;
            for (int i = 0; i < tail.Length; i++)
            {
                byte b = tail[i];
                if ((b & 0b1100_0000) != 0b1000_0000) { start = i; break; }
            }
            var safeTail = enc.GetString(tail[start..]);
            buf.Clear();
            buf.Append(safeTail);
            if (!buf.ToString().EndsWith("\n\n")) buf.Append("\n\n");
        }
        else buf.Clear();

        // Add candidate (may still overflow; let next iteration handle)
        if (buf.Length > 0 && !buf.ToString().EndsWith("\n\n")) buf.Append("\n\n");
        buf.Append(cand);

        if (enc.GetByteCount(buf.ToString()) > maxBytes)
        {
            // Hard trim to byte budget without breaking codepoints
            var s = buf.ToString();
            int hi = s.Length, lo = 0, ok = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                var sub = s.Substring(0, mid);
                var n = enc.GetByteCount(sub);
                if (n <= maxBytes) { ok = mid; lo = mid + 1; } else hi = mid - 1;
            }
            var emit = s.Substring(0, ok);
            yield return emit;
            buf.Clear();
            var rem = s.Substring(ok);
            buf.Append(rem);
        }
    }
}
