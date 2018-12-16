using System.Collections.Generic;

public class Room
{
    public const int MaxClients = 2;

    public string Id { get; private set; }

    public int NumClients
    {
        get { return _clients.Count; }
    }

    public IEnumerable<int> ClientIds
    {
        get { return _clients; }
    }

    private readonly HashSet<int> _clients = new HashSet<int>();

    public Room(string id)
    {
        Id = id;
    }

    public bool AddClient(int clientId)
    {
        if (_clients.Count >= MaxClients || _clients.Contains(clientId))
            return false;
        _clients.Add(clientId);
        return true;
    }

    public bool RemoveClient(int clientId)
    {
        return _clients.Remove(clientId);
    }
}
