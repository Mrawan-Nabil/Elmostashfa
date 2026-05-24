using System.Collections.Generic;
using UnityEngine;

public class PathManager : MonoBehaviour
{
    [System.Serializable]
    public class RoomPath
    {
        public string roomName;

        [Tooltip("First element MUST be a MAIN PATH node")]
        public List<Transform> path;
    }

    [Header("Main Path (ordered)")]
    public List<Transform> mainPath;

    [Header("Rooms")]
    public List<RoomPath> rooms;

    // MAIN PATH FUNCTION
    public List<Transform> GetPath(string fromName, string toName)
    {
        RoomPath toRoom = GetRoom(toName);

        if (toRoom == null)
        {
            Debug.LogError("Target room not found: " + toName);
            return null;
        }

        List<Transform> result = new List<Transform>();

        Transform startMain = GetStartMainNode(fromName, result);

        if (startMain == null)
        {
            Debug.LogError("Start node not found: " + fromName);
            return null;
        }

        Transform targetMain = toRoom.path[0];

        int startIndex = mainPath.IndexOf(startMain);
        int targetIndex = mainPath.IndexOf(targetMain);

        if (startIndex == -1 || targetIndex == -1)
        {
            Debug.LogError("Main path connection missing!");
            return result;
        }

        // 🔁 move on main path
        if (startIndex < targetIndex)
        {
            for (int i = startIndex + 1; i <= targetIndex; i++)
                result.Add(mainPath[i]);
        }
        else
        {
            for (int i = startIndex - 1; i >= targetIndex; i--)
                result.Add(mainPath[i]);
        }

        // 🏠 enter room
        for (int i = 1; i < toRoom.path.Count; i++)
            result.Add(toRoom.path[i]);

        return result;
    }

    // 🧠 Handles BOTH room OR main node
    private Transform GetStartMainNode(string name, List<Transform> result)
    {
        RoomPath fromRoom = GetRoom(name);

        if (fromRoom != null)
        {
            // go back from room to main
            for (int i = fromRoom.path.Count - 1; i >= 0; i--)
                result.Add(fromRoom.path[i]);

            return fromRoom.path[0];
        }

        // not a room → treat as main path node (spawnPoint case)
        return FindMainNode(name);
    }

    private Transform FindMainNode(string name)
    {
        return mainPath.Find(p => p.name == name);
    }

    public string GetRandomRoom()
    {
        if (rooms.Count == 0) return null;
        return rooms[Random.Range(0, rooms.Count)].roomName;
    }

    public RoomPath GetRoom(string name)
    {
        return rooms.Find(r => r.roomName == name);
    }
}