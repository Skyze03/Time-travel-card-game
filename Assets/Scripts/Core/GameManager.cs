using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Card Database")]
    public List<CardData> allCards = new List<CardData>();

    [Header("UI")]
    public GameUIManager uiManager;

    [Header("Runtime Players")]
    public PlayerState player1;
    public PlayerState player2;

    [Header("Game State")]
    public int currentRound = 0;
    public int maxRounds = 12;

    private CardData selectedPlayer1Card;
    private int selectedPlayer1HandIndex = -1;

    private void Start()
    {
        StartNewGame();
    }

    public void StartNewGame()
    {
        player1 = new PlayerState("Player 1");
        player2 = new PlayerState("Player 2");

        DealStartingHand(player1);
        DealStartingHand(player2);

        currentRound = 0;
        selectedPlayer1Card = null;
        selectedPlayer1HandIndex = -1;

        RefreshAllUI();

        Debug.Log("Game started.");
        Debug.Log(player1.playerId + " hand count: " + player1.hand.Count);
        Debug.Log(player2.playerId + " hand count: " + player2.hand.Count);
    }

    private void DealStartingHand(PlayerState player)
    {
        player.hand.Clear();

        CardData card2 = FindCard(CardRank.Two);
        CardData card3 = FindCard(CardRank.Three);
        CardData card4 = FindCard(CardRank.Four);

        player.hand.Add(card2);
        player.hand.Add(card3);
        player.hand.Add(card4);
        player.hand.Add(card2);

        for (int i = 0; i < 8; i++)
        {
            CardData randomCard = allCards[Random.Range(0, allCards.Count)];
            player.hand.Add(randomCard);
        }
    }

    private CardData FindCard(CardRank rank)
    {
        foreach (CardData card in allCards)
        {
            if (card.rank == rank)
            {
                return card;
            }
        }

        Debug.LogError("Missing card data for rank: " + rank);
        return null;
    }

    public void OnPlayer1CardSelected(int handIndex)
    {
        if (handIndex < 0 || handIndex >= player1.hand.Count)
        {
            return;
        }

        selectedPlayer1HandIndex = handIndex;
        selectedPlayer1Card = player1.hand[handIndex];

        Debug.Log("Player 1 selected: " + selectedPlayer1Card.displayName);

        if (uiManager != null)
        {
            uiManager.SetRevealText(
                "You: " + selectedPlayer1Card.displayName,
                "Opponent: Waiting..."
            );
        }
    }

    private void RefreshAllUI()
    {
        if (uiManager == null)
        {
            return;
        }

        uiManager.SetRoundText(currentRound, maxRounds);
        uiManager.BuildTimelineUI(uiManager.player2TimelineParent, player2);
        uiManager.BuildTimelineUI(uiManager.player1TimelineParent, player1);
        uiManager.BuildOpponentHandUI(player2.hand.Count);
        uiManager.BuildPlayerHandUI(player1, this);
        uiManager.SetRevealText("You: None", "Opponent: Hidden");
    }
}