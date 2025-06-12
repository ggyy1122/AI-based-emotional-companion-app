using HandAngleDemo;

public class HandGestureManager
{
    private static readonly HandGestureManager _instance = new HandGestureManager();
    public static HandGestureManager Instance => _instance;

    private HandGesture? _handGesture;
    private bool _isReady = false;

    public bool IsReady => _isReady;
    public HandGesture? HandGestureInstance => _handGesture;

    private HandGestureManager() { }

    public void Preload()
    {
        if (_handGesture == null && !_isReady)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                _handGesture = new HandGesture();
                _handGesture.StartCamera();
                _isReady = true;
            });
        }
    }
}