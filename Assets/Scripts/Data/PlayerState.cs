using System.Collections.Generic;

[System.Serializable]
public class PlayerState
{
    public string playerId;
    public List<CardData> hand = new List<CardData>();
    public TimelineSlot[] timeline = new TimelineSlot[12];

    public int coins;
    public bool hasActiveCamera;
    public int forcedNextSlotIndex;
    public List<int> timePointSlots = new List<int>();

    public PlayerState(string id)
    {
        playerId = id;
        coins = 0;
        hasActiveCamera = false;
        forcedNextSlotIndex = -1;

        for (int i = 0; i < 12; i++)
        {
            timeline[i] = new TimelineSlot(i);
        }
    }

    public bool HasUsableTimePoint()
    {
        return timePointSlots.Count > 0;
    }

    public int GetEarliestUsableTimePoint()
    {
        if (timePointSlots.Count == 0)
        {
            return -1;
        }

        timePointSlots.Sort();
        return timePointSlots[0];
    }
}