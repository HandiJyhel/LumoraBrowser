using Microsoft.UI.Dispatching;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace Lumora.WinUI.ReadAloud;

internal enum ReadAloudState { Stopped, Playing, Paused }

// Lecture a voix haute du texte d'une page, via la synthese vocale native de
// Windows (Windows.Media.SpeechSynthesis) : 100% locale, aucun texte de page
// n'est envoye a un serveur externe. Les blocs de texte sont synthetises et
// lus l'un apres l'autre (pas un seul long flux) pour permettre un arret net
// et eviter les limites de longueur d'un appel unique.
internal sealed class ReadAloudService : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly MediaPlayer _player = new();
    private IReadOnlyList<string> _chunks = [];
    private int _index;

    public ReadAloudState State { get; private set; } = ReadAloudState.Stopped;
    public event Action<ReadAloudState>? StateChanged;

    public ReadAloudService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _player.MediaEnded += Player_MediaEnded;
    }

    public void Start(IReadOnlyList<string> chunks)
    {
        Stop();
        _chunks = chunks;
        _index = 0;
        if (_chunks.Count == 0) return;
        _ = PlayCurrentChunkAsync();
    }

    public void Pause()
    {
        if (State != ReadAloudState.Playing) return;
        _player.Pause();
        SetState(ReadAloudState.Paused);
    }

    public void Resume()
    {
        if (State != ReadAloudState.Paused) return;
        _player.Play();
        SetState(ReadAloudState.Playing);
    }

    public void Stop()
    {
        _player.Pause();
        _player.Source = null;
        _index = 0;
        _chunks = [];
        if (State != ReadAloudState.Stopped) SetState(ReadAloudState.Stopped);
    }

    private async Task PlayCurrentChunkAsync()
    {
        if (_index >= _chunks.Count) { Stop(); return; }

        try
        {
            var stream = await _synthesizer.SynthesizeTextToStreamAsync(_chunks[_index]);
            _player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
            _player.Play();
            SetState(ReadAloudState.Playing);
        }
        catch
        {
            Stop();
        }
    }

    private void Player_MediaEnded(MediaPlayer sender, object args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (State == ReadAloudState.Stopped) return; // arrêt demandé pendant la lecture du morceau
            _index++;
            _ = PlayCurrentChunkAsync();
        });
    }

    private void SetState(ReadAloudState state)
    {
        State = state;
        _dispatcherQueue.TryEnqueue(() => StateChanged?.Invoke(state));
    }

    public void Dispose()
    {
        _player.MediaEnded -= Player_MediaEnded;
        _player.Dispose();
        _synthesizer.Dispose();
    }
}
