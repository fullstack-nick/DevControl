namespace DevControl.Sdk;

public sealed class DevControlClient
{
    public DevControlClient(Uri baseAddress)
    {
        BaseAddress = baseAddress ?? throw new ArgumentNullException(nameof(baseAddress));
    }

    public Uri BaseAddress { get; }
}

