using System.Net;
using Elements.Core;
using Newtonsoft.Json;

namespace ResoHelperFP;

public class SessionDataReceiver
{
    private readonly HttpListener _listener = new();

    public event Action<string, Dictionary<string, SessionData>>? SessionsUpdated;


    public SessionDataReceiver()
    {
        _listener.Prefixes.Add("http://localhost:9393/ingest/");
        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Stop();
    }

    public async Task HandleConnection()
    {
        var context = await _listener.GetContextAsync();
        var hostname = context.Request.Url?.Segments.Last();
        if (hostname is "ingest/" or null or "")
        {
            await Respond(context.Response, "Failure"u8.ToArray());
            return;
        }

        var body = await new StreamReader(context.Request.InputStream).ReadToEndAsync();
        UniLog.Log($"Received session data: {body}");
        var data = JsonConvert.DeserializeObject<Dictionary<string, SessionData>>(body) ??
                   new Dictionary<string, SessionData>();
        SessionsUpdated?.Invoke(hostname ?? "Unkown", data);
        await Respond(context.Response, "Success"u8.ToArray());
    }

    private async Task Respond(HttpListenerResponse response, byte[] buffer)
    {
        response.ContentLength64 = buffer.Length;
        var output = response.OutputStream;
        await output.WriteAsync(buffer);
        output.Close();
    }
}