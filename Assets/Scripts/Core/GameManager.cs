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
    public GamePhase currentPhase = GamePhase.TurnPlanning;

    private CardData selectedPlayer1Card;
    private int selectedPlayer1HandIndex = -1;
    private int selectedTargetSlotIndex = -1;

    
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
        currentPhase = GamePhase.TurnPlanning;
        player1.coins = 0;
        player2.coins = 0;

        player1.hasActiveCamera = false;
        player2.hasActiveCamera = false;  

        selectedPlayer1Card = null;
        selectedPlayer1HandIndex = -1;
        selectedTargetSlotIndex = -1;

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
        if (currentPhase != GamePhase.TurnPlanning)
        {
            return;
        }

        if (handIndex < 0 || handIndex >= player1.hand.Count)
        {
            return;
        }

        selectedPlayer1HandIndex = handIndex;
        selectedPlayer1Card = player1.hand[handIndex];

        Debug.Log("Player 1 selected card: " + selectedPlayer1Card.displayName);

        if (uiManager != null)
        {
            string slotText = selectedTargetSlotIndex >= 0 ? $"Slot {selectedTargetSlotIndex + 1}" : "No Slot";
            uiManager.SetRevealText(
                $"You: {selectedPlayer1Card.displayName} -> {slotText}",
                "Opponent: Hidden"
            );

            RefreshAllUI();
        }
    }

    public bool IsSlotLockedForDisplay(PlayerState player, int slotIndex)
    {
        return IsSlotLockedByBarrier(player, slotIndex);
    }
    private void RefreshAllUI()
    {
        if (uiManager == null)
        {
            return;
        }

        uiManager.SetRoundText(currentRound < maxRounds ? currentRound : maxRounds - 1, maxRounds);
        uiManager.BuildTimelineUI(uiManager.player2TimelineParent, player2, this, false);
        uiManager.BuildTimelineUI(uiManager.player1TimelineParent, player1, this, true);
        uiManager.BuildOpponentHandUI(player2.hand.Count);
        uiManager.BuildPlayerHandUI(player1, this);

        if (currentPhase == GamePhase.GameEnded)
        {
            string resultText;

            if (player1.coins > player2.coins)
            {
                resultText = "You Win!";
            }
            else if (player2.coins > player1.coins)
            {
                resultText = "Opponent Wins!";
            }
            else
            {
                resultText = "Draw!";
            }

            uiManager.SetRevealText(
                $"You: {player1.coins} coins",
                $"Opponent: {player2.coins} coins\n{resultText}"
            );
        }
        else
        {
            if (currentPhase == GamePhase.FinalResolution)
            {
                uiManager.SetRevealText(
                    "All turns complete",
                    "Press Resolve to score"
                );
            }
            else
            {
                string cardText = selectedPlayer1Card != null ? selectedPlayer1Card.displayName : "None";
                string slotText = selectedTargetSlotIndex >= 0 ? $"Slot {selectedTargetSlotIndex + 1}" : "No Slot";
                string forcedText = HasForcedNextSlot(player1) ? $" | Forced Slot {player1.forcedNextSlotIndex + 1}" : "";

                uiManager.SetRevealText(
                    $"You: {cardText} -> {slotText}{forcedText}",
                    "Opponent: Hidden"
                );
            }
        }
    }
    public void RestartGame()
    {
        Debug.Log("Restarting game...");
        StartNewGame();
    }
    public bool IsSlotSelectableForCurrentTurn(int slotIndex)
    {
        if (currentPhase != GamePhase.TurnPlanning)
        {
            return false;
        }

        if (slotIndex < 0 || slotIndex >= player1.timeline.Length)
        {
            return false;
        }

        // 还没选牌时，不允许选 slot
        if (selectedPlayer1Card == null)
        {
            return false;
        }

        int currentTurnSlot = currentRound;

        // 未来 slot 永远不可选
        if (slotIndex > currentTurnSlot)
        {
            return false;
        }

        // Court 强制下一回合目标 slot：优先级最高
        if (HasForcedNextSlot(player1))
        {
            return slotIndex == player1.forcedNextSlotIndex;
        }

        // Barrier 之前的 slot 直接不可选
        if (IsSlotLockedByBarrier(player1, slotIndex))
        {
            return false;
        }

        // 当前回合对应的 slot 永远可以正常打
        if (slotIndex == currentTurnSlot)
        {
            return true;
        }

        // 如果要回填到过去 slot，必须存在一个“更早的可用 Time Point”
        return HasUsableTimePointBeforeSlot(player1, slotIndex);
    }

    private void ResetAllJokerCancellationFlags(PlayerState player)
    {
        for (int i = 0; i < player.timeline.Length; i++)
        {
            if (!player.timeline[i].IsEmpty && player.timeline[i].currentCard != null)
            {
                player.timeline[i].currentCard.isCancelledByJoker = false;
            }
        }
    }
    private bool IsCardCancelledAtSlot(PlayerState player, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= player.timeline.Length)
        {
            return false;
        }

        if (player.timeline[slotIndex].IsEmpty)
        {
            return false;
        }

        PlayedCard playedCard = player.timeline[slotIndex].currentCard;

        if (playedCard == null)
        {
            return false;
        }

        return playedCard.isCancelledByJoker;
    }
    private bool IsTimePointCard(CardData card)
    {
        if (card == null) return false;

        return card.effectType == CardEffectType.SetTimePoint;
    }

    private bool IsBarrierCard(CardData card)
    {
        if (card == null) return false;

        return card.effectType == CardEffectType.Barrier;
    }
    private bool IsRobCard(CardData card)
    {
        if (card == null) return false;

        return card.effectType == CardEffectType.Rob;
    }
    private bool IsCameraCard(CardData card)
    {
        if (card == null) return false;

        return card.effectType == CardEffectType.Camera;
    }
    private bool IsCourtCard(CardData card)
    {
        if (card == null) return false;

        return card.effectType == CardEffectType.Court;
    }
    private bool IsJokerCard(CardData card)
    {
        if (card == null) return false;

        return card.effectType == CardEffectType.Joker;
    }
    private bool IsCardEffectivelyCancelledByJoker(PlayerState self, PlayerState opponent, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxRounds)
        {
            return false;
        }

        if (self.timeline[slotIndex].IsEmpty)
        {
            return false;
        }

        if (opponent.timeline[slotIndex].IsEmpty)
        {
            return false;
        }

        CardData opponentCard = opponent.timeline[slotIndex].currentCard.card;

        return IsJokerCard(opponentCard);
    }
    private bool DoesPlayerHaveActiveCourtAtSlot(PlayerState self, PlayerState opponent, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxRounds)
        {
            return false;
        }

        if (self.timeline[slotIndex].IsEmpty)
        {
            return false;
        }

        CardData selfCard = self.timeline[slotIndex].currentCard.card;

        if (!IsCourtCard(selfCard))
        {
            return false;
        }

        if (IsCardEffectivelyCancelledByJoker(self, opponent, slotIndex))
        {
            return false;
        }

        return true;
    }  
    private int GetForcedNextSlotFromThisRound(int roundSlotIndex)
    {
        if (roundSlotIndex < 0 || roundSlotIndex >= maxRounds)
        {
            return -1;
        }

        bool player1CourtActive = DoesPlayerHaveActiveCourtAtSlot(player1, player2, roundSlotIndex);
        bool player2CourtActive = DoesPlayerHaveActiveCourtAtSlot(player2, player1, roundSlotIndex);

        if (!player1CourtActive && !player2CourtActive)
        {
            return -1;
        }

        int nextSlot = roundSlotIndex + 1;

        if (nextSlot >= maxRounds)
        {
            return -1;
        }

        return nextSlot;
    }
    private void RebuildTimePointSlots(PlayerState player)
    {
        player.timePointSlots.Clear();

        PlayerState opponent = GetOpponent(player);

        for (int i = 0; i < player.timeline.Length; i++)
        {
            if (!player.timeline[i].IsEmpty)
            {
                CardData card = player.timeline[i].currentCard.card;

                if (IsTimePointCard(card) && !IsCardEffectivelyCancelledByJoker(player, opponent, i))
                {
                    player.timePointSlots.Add(i);
                }
            }
        }
    }
    private PlayerState GetOpponent(PlayerState player)
    {
        if (player == player1)
        {
            return player2;
        }

        return player1;
    }
    /*private int GetEarliestBarrierSlot(PlayerState player)
    {
        for (int i = 0; i < player.timeline.Length; i++)
        {
            if (!player.timeline[i].IsEmpty)
            {
                CardData card = player.timeline[i].currentCard.card;

                if (IsBarrierCard(card))
                {
                    return i;
                }
            }
        }

        return -1;
    }*/
    /*private int GetGlobalEarliestBarrierSlot()
    {
        int best = -1;

        for (int i = 0; i < maxRounds; i++)
        {
            bool player1HasBarrier = false;
            bool player2HasBarrier = false;

            if (!player1.timeline[i].IsEmpty)
            {
                CardData p1Card = player1.timeline[i].currentCard.card;
                player1HasBarrier = IsBarrierCard(p1Card);
            }

            if (!player2.timeline[i].IsEmpty)
            {
                CardData p2Card = player2.timeline[i].currentCard.card;
                player2HasBarrier = IsBarrierCard(p2Card);
            }

            if (player1HasBarrier || player2HasBarrier)
            {
                best = i;
                break;
            }
        }

        return best;
    }*/
    private int GetLatestGlobalBarrierSlot()
    {
        int best = -1;

        for (int i = 0; i < maxRounds; i++)
        {
            bool player1HasActiveBarrier = false;
            bool player2HasActiveBarrier = false;

            if (!player1.timeline[i].IsEmpty)
            {
                CardData p1Card = player1.timeline[i].currentCard.card;

                player1HasActiveBarrier =
                    IsBarrierCard(p1Card) &&
                    !IsCardEffectivelyCancelledByJoker(player1, player2, i);
            }

            if (!player2.timeline[i].IsEmpty)
            {
                CardData p2Card = player2.timeline[i].currentCard.card;

                player2HasActiveBarrier =
                    IsBarrierCard(p2Card) &&
                    !IsCardEffectivelyCancelledByJoker(player2, player1, i);
            }

            if (player1HasActiveBarrier || player2HasActiveBarrier)
            {
                best = i;
            }
        }

        return best;
    }
    private int GetLatestGlobalBarrierSlotUpTo(int maxSlotInclusive)
    {
        int best = -1;

        int cappedMax = Mathf.Min(maxSlotInclusive, maxRounds - 1);

        for (int i = 0; i <= cappedMax; i++)
        {
            bool player1HasActiveBarrier = false;
            bool player2HasActiveBarrier = false;

            if (!player1.timeline[i].IsEmpty)
            {
                CardData p1Card = player1.timeline[i].currentCard.card;

                player1HasActiveBarrier =
                    IsBarrierCard(p1Card) &&
                    !IsCardEffectivelyCancelledByJoker(player1, player2, i);
            }

            if (!player2.timeline[i].IsEmpty)
            {
                CardData p2Card = player2.timeline[i].currentCard.card;

                player2HasActiveBarrier =
                    IsBarrierCard(p2Card) &&
                    !IsCardEffectivelyCancelledByJoker(player2, player1, i);
            }

            if (player1HasActiveBarrier || player2HasActiveBarrier)
            {
                best = i;
            }
        }

        return best;
    }
    private int GetEarliestUsableTimePointSlot(PlayerState player)
    {
        if (player.timePointSlots == null || player.timePointSlots.Count == 0)
        {
            return -1;
        }

        int latestGlobalBarrier = GetLatestGlobalBarrierSlot();
        int best = -1;

        for (int i = 0; i < player.timePointSlots.Count; i++)
        {
            int tpSlot = player.timePointSlots[i];

            // 最新 Barrier 之前的 Time Point 都不再 usable
            if (latestGlobalBarrier >= 0 && tpSlot < latestGlobalBarrier)
            {
                continue;
            }

            if (best == -1 || tpSlot < best)
            {
                best = tpSlot;
            }
        }

        return best;
    }

    private bool HasUsableTimePointBeforeSlot(PlayerState player, int targetSlot)
    {
        if (player.timePointSlots == null || player.timePointSlots.Count == 0)
        {
            return false;
        }

        int latestGlobalBarrier = GetLatestGlobalBarrierSlot();

        for (int i = 0; i < player.timePointSlots.Count; i++)
        {
            int tpSlot = player.timePointSlots[i];

            // 必须严格早于目标 slot
            if (tpSlot >= targetSlot)
            {
                continue;
            }

            // 被全局 Barrier 挡在前面的 TP 不可用
            if (latestGlobalBarrier >= 0 && tpSlot < latestGlobalBarrier)
            {
                continue;
            }

            return true;
        }

        return false;
    }
    private bool HasUsableTimePoint(PlayerState player)
    {
        return GetEarliestUsableTimePointSlot(player) >= 0;
    }
    private bool HasForcedNextSlot(PlayerState player)
    {
        return player.forcedNextSlotIndex >= 0 && player.forcedNextSlotIndex < maxRounds;
    }
    private void ConsumeForcedSlotIfMatched(PlayerState player, int playedSlot)
    {
        if (player.forcedNextSlotIndex == playedSlot)
        {
            player.forcedNextSlotIndex = -1;
        }
    }
    private void SetForcedNextSlotForBothPlayers(int forcedSlot)
    {
        if (forcedSlot < 0 || forcedSlot >= maxRounds)
        {
            return;
        }

        player1.forcedNextSlotIndex = forcedSlot;
        player2.forcedNextSlotIndex = forcedSlot;

        Debug.Log($"Both players are forced next turn at slot {forcedSlot + 1}");
    }
    /*private void ApplyCourtForceIfNeeded(CardData playedCard, int playedSlot)
    {
        if (!IsCourtCard(playedCard))
        {
            return;
        }

        int forcedSlot = playedSlot + 1;

        if (forcedSlot < maxRounds)
        {
            player1.forcedNextSlotIndex = forcedSlot;
            player2.forcedNextSlotIndex = forcedSlot;

            Debug.Log($"Court played. Both players are forced next turn at slot {forcedSlot + 1}");
        }
    }*/
    private bool HasUsableTimePointAtResolution(PlayerState player, int currentResolvingSlot)
    {
        int latestBarrierUpToCurrentStep = GetLatestGlobalBarrierSlotUpTo(currentResolvingSlot);

        for (int i = 0; i <= currentResolvingSlot; i++)
        {
            if (!player.timeline[i].IsEmpty)
            {
                CardData card = player.timeline[i].currentCard.card;

                if (IsTimePointCard(card))
                {
                    // 只考虑当前 resolve step 以及之前已经发生的 Barrier
                    if (latestBarrierUpToCurrentStep >= 0 && i < latestBarrierUpToCurrentStep)
                    {
                        continue;
                    }

                    return true;
                }
            }
        }

        return false;
    }
    
    private int GetCardPointValueAtSlot(PlayerState player, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= player.timeline.Length)
        {
            return 0;
        }

        if (player.timeline[slotIndex].IsEmpty)
        {
            return 0;
        }

        CardData card = player.timeline[slotIndex].currentCard.card;

        if (card == null)
        {
            return 0;
        }

        return card.pointValue;
    }
    public void OnPlayer1TargetSlotSelected(int slotIndex)
    {
        if (currentPhase != GamePhase.TurnPlanning)
        {
            return;
        }

        if (!IsSlotSelectableForCurrentTurn(slotIndex))
        {
            return;
        }

        selectedTargetSlotIndex = slotIndex;

        Debug.Log("Player 1 target slot selected: " + (slotIndex + 1));

        if (uiManager != null)
        {
            string cardText = selectedPlayer1Card != null ? selectedPlayer1Card.displayName : "None";
            uiManager.SetRevealText(
                $"You: {cardText} -> Slot {slotIndex + 1}",
                "Opponent: Hidden"
            );
        }
    }

    private bool IsSlotLockedByBarrier(PlayerState player, int slotIndex)
    {
        int latestGlobalBarrier = GetLatestGlobalBarrierSlot();

        if (latestGlobalBarrier < 0)
        {
            return false;
        }

        // 最新 Barrier 之前的所有 slot 都被锁
        return slotIndex < latestGlobalBarrier;
    }

    public void ConfirmPlayer1Placement()
    {
        if (selectedPlayer1Card == null || selectedPlayer1HandIndex < 0 || selectedTargetSlotIndex < 0)
        {
            Debug.Log("Cannot confirm: card or slot not selected.");
            return;
        }

        if (!IsSlotSelectableForCurrentTurn(selectedTargetSlotIndex))
        {
            Debug.Log("Cannot confirm: selected slot is not legal.");
            return;
        }

        // 记录玩家本回合打出的牌和位置
        CardData playerPlayedCard = selectedPlayer1Card;
        int playerPlayedSlot = selectedTargetSlotIndex;

        // 玩家放牌
        PlayedCard playerPlayed = new PlayedCard(
            playerPlayedCard,
            currentRound,
            playerPlayedSlot,
            player1
        );

        player1.timeline[playerPlayedSlot].currentCard = playerPlayed;

        // 更新玩家 timeline 上的 time point 状态
        RebuildTimePointSlots(player1);

        // 从手牌移除
        player1.hand.RemoveAt(selectedPlayer1HandIndex);

        // 对手本回合也出牌，但这里只是“完成本回合 placement”
        CardData opponentPlayedCard;
        int opponentPlayedSlot;
        AutoPlayForPlayer2(out opponentPlayedCard, out opponentPlayedSlot);

        // Step 1: 先消费旧 forced（本回合如果刚好打在 forced slot 上，就表示已经履行）
        ConsumeForcedSlotIfMatched(player1, playerPlayedSlot);
        ConsumeForcedSlotIfMatched(player2, opponentPlayedSlot);
        /*
        // Step 2: 再根据“本回合刚打出的 Court”创建下一回合 forced
        int nextForcedSlot = -1;

        if (IsCourtCard(playerPlayedCard))
        {
            int candidate = playerPlayedSlot + 1;
            if (candidate < maxRounds)
            {
                nextForcedSlot = candidate;
            }
        }

        if (IsCourtCard(opponentPlayedCard))
        {
            int candidate = opponentPlayedSlot + 1;
            if (candidate < maxRounds)
            {
                // 如果双方这回合都打出 Court，并且产生不同 candidate，
                // 目前先取较大的那个。这个规则以后可以再和 Mac 微调。
                if (nextForcedSlot == -1 || candidate > nextForcedSlot)
                {
                    nextForcedSlot = candidate;
                }
            }
        }

        if (nextForcedSlot >= 0)
        {
            SetForcedNextSlotForBothPlayers(nextForcedSlot);
        }
        */

        // Step 2: 再根据“本回合这个 slot 上是否存在有效 Court”来决定下一回合 forced slot
        int nextForcedSlot = GetForcedNextSlotFromThisRound(currentRound);

        if (nextForcedSlot >= 0)
        {
            SetForcedNextSlotForBothPlayers(nextForcedSlot);
        }
        // 回合前进
        currentRound++;

        // 如果已经完成 12 回合，进入最终结算阶段
        if (currentRound >= maxRounds)
        {
            currentPhase = GamePhase.FinalResolution;
        }

        // 清空本回合选择
        selectedPlayer1Card = null;
        selectedPlayer1HandIndex = -1;
        selectedTargetSlotIndex = -1;

        RefreshAllUI();
    }

    private void AutoPlayForPlayer2(out CardData opponentPlayedCard, out int opponentPlayedSlot)
    {
        opponentPlayedCard = null;
        opponentPlayedSlot = -1;

        if (player2.hand.Count == 0) return;

        int randomIndex = Random.Range(0, player2.hand.Count);
        CardData opponentCard = player2.hand[randomIndex];

        // 这里只能读取“本回合开始时已经存在的 forced slot”
        int opponentSlot = HasForcedNextSlot(player2) ? player2.forcedNextSlotIndex : currentRound;

        PlayedCard playedCard = new PlayedCard(
            opponentCard,
            currentRound,
            opponentSlot,
            player2
        );

        player2.timeline[opponentSlot].currentCard = playedCard;

        // 这里只负责放牌，不要在这里消费 forced，也不要在这里创建新的 Court forced
        RebuildTimePointSlots(player2);

        player2.hand.RemoveAt(randomIndex);

        opponentPlayedCard = opponentCard;
        opponentPlayedSlot = opponentSlot;
    }

    public void OnMainActionButtonPressed()
    {
        if (currentPhase == GamePhase.TurnPlanning)
        {
            ConfirmPlayer1Placement();
        }
        else if (currentPhase == GamePhase.FinalResolution)
        {
            ResolveEntireGame();
        }
    }
    private int GetBaseCoinGainForCardAtSlot(PlayerState player, int slotIndex)
    {
        if (player.timeline[slotIndex].IsEmpty)
        {
            return 0;
        }

        if (IsCardCancelledAtSlot(player, slotIndex))
        {
            Debug.Log($"{player.playerId}'s card at slot {slotIndex + 1} is cancelled by Joker");
            return 0;
        }

        CardData card = player.timeline[slotIndex].currentCard.card;

        switch (card.effectType)
        {
            case CardEffectType.GainCoins:
                Debug.Log($"{player.playerId} base gain +5 from slot {slotIndex + 1}");
                return 5;

            case CardEffectType.Lottery:
                if (HasUsableTimePointAtResolution(player, slotIndex))
                {
                    Debug.Log($"{player.playerId} base gain +10 from Lottery at slot {slotIndex + 1}");
                    return 10;
                }
                else
                {
                    Debug.Log($"{player.playerId} Lottery failed at slot {slotIndex + 1}");
                    return 0;
                }

            case CardEffectType.SetTimePoint:
            case CardEffectType.Barrier:
            case CardEffectType.None:
                return 0;

            case CardEffectType.Rob:
            case CardEffectType.Camera:
            case CardEffectType.Court:
            case CardEffectType.Joker:
                return 0;
        }

        return 0;
    }
    
    private void ResolveSingleSlot(int slotIndex)
    {
        Debug.Log($"--- Resolving slot {slotIndex + 1} ---");
        
        // Step 0: 先处理 Joker（最高优先级）
        bool player1HasJoker = false;
        bool player2HasJoker = false;

        if (!player1.timeline[slotIndex].IsEmpty)
        {
            CardData p1Card = player1.timeline[slotIndex].currentCard.card;
            player1HasJoker = IsJokerCard(p1Card);
        }

        if (!player2.timeline[slotIndex].IsEmpty)
        {
            CardData p2Card = player2.timeline[slotIndex].currentCard.card;
            player2HasJoker = IsJokerCard(p2Card);
        }

        // Player 1 的 Joker 取消 Player 2 同格牌
        if (player1HasJoker && !player2.timeline[slotIndex].IsEmpty)
        {
            player2.timeline[slotIndex].currentCard.isCancelledByJoker = true;
            Debug.Log($"Player 1's Joker cancels Player 2's card at slot {slotIndex + 1}");
        }

        // Player 2 的 Joker 取消 Player 1 同格牌
        if (player2HasJoker && !player1.timeline[slotIndex].IsEmpty)
        {
            player1.timeline[slotIndex].currentCard.isCancelledByJoker = true;
            Debug.Log($"Player 2's Joker cancels Player 1's card at slot {slotIndex + 1}");
        }

        SlotResolutionData slotData = new SlotResolutionData();

        // Step 1: 先计算双方这个 slot 的基础收益
        slotData.player1SlotGain = GetBaseCoinGainForCardAtSlot(player1, slotIndex);
        slotData.player2SlotGain = GetBaseCoinGainForCardAtSlot(player2, slotIndex);

        // Step 2: 本格若有 Camera，先激活
        if (!player1.timeline[slotIndex].IsEmpty && !IsCardCancelledAtSlot(player1, slotIndex))
        {
            CardData p1Card = player1.timeline[slotIndex].currentCard.card;

            if (IsCameraCard(p1Card))
            {
                player1.hasActiveCamera = true;
                Debug.Log($"Player 1 activates Camera at slot {slotIndex + 1}");
            }
        }

        if (!player2.timeline[slotIndex].IsEmpty && !IsCardCancelledAtSlot(player2, slotIndex))
        {
            CardData p2Card = player2.timeline[slotIndex].currentCard.card;

            if (IsCameraCard(p2Card))
            {
                player2.hasActiveCamera = true;
                Debug.Log($"Player 2 activates Camera at slot {slotIndex + 1}");
            }
        }

        // Step 3: 处理 Player 1 的 Rob
        if (!player1.timeline[slotIndex].IsEmpty && !IsCardCancelledAtSlot(player1, slotIndex))
        {
            CardData p1Card = player1.timeline[slotIndex].currentCard.card;

            if (IsRobCard(p1Card))
            {
                if (player2.hasActiveCamera)
                {
                    player2.hasActiveCamera = false;
                    Debug.Log($"Player 2's Camera blocks Player 1's Rob at slot {slotIndex + 1}");
                }
                else
                {
                    int stolen = slotData.player2SlotGain;
                    slotData.player2SlotGain -= stolen;
                    slotData.player1SlotGain += stolen;

                    Debug.Log($"Player 1 robs {stolen} coins from Player 2 at slot {slotIndex + 1}");
                }
            }
        }

        // Step 4: 处理 Player 2 的 Rob
        if (!player2.timeline[slotIndex].IsEmpty && !IsCardCancelledAtSlot(player2, slotIndex))
        {
            CardData p2Card = player2.timeline[slotIndex].currentCard.card;

            if (IsRobCard(p2Card))
            {
                if (player1.hasActiveCamera)
                {
                    player1.hasActiveCamera = false;
                    Debug.Log($"Player 1's Camera blocks Player 2's Rob at slot {slotIndex + 1}");
                }
                else
                {
                    int stolen = slotData.player1SlotGain;
                    slotData.player1SlotGain -= stolen;
                    slotData.player2SlotGain += stolen;

                    Debug.Log($"Player 2 robs {stolen} coins from Player 1 at slot {slotIndex + 1}");
                }
            }
        }

        // Step 5: 处理 Court（由前一格触发，比较当前格）
        int previousSlot = slotIndex - 1;

        if (previousSlot >= 0)
        {
            bool player1HasCourt = false;
            bool player2HasCourt = false;

            if (!player1.timeline[previousSlot].IsEmpty &&
                !IsCardEffectivelyCancelledByJoker(player1, player2, previousSlot))
            {
                CardData p1PrevCard = player1.timeline[previousSlot].currentCard.card;
                player1HasCourt = IsCourtCard(p1PrevCard);
            }

            if (!player2.timeline[previousSlot].IsEmpty &&
                !IsCardEffectivelyCancelledByJoker(player2, player1, previousSlot))
            {
                CardData p2PrevCard = player2.timeline[previousSlot].currentCard.card;
                player2HasCourt = IsCourtCard(p2PrevCard);
            }

            int p1Point = GetCardPointValueAtSlot(player1, slotIndex);
            int p2Point = GetCardPointValueAtSlot(player2, slotIndex);

            // Player 1 发起 Court
            if (player1HasCourt)
            {
                if (p1Point > p2Point)
                {
                    slotData.player1SlotGain += 10;
                    Debug.Log($"Player 1 wins Court at slot {slotIndex + 1} and gains +10");
                }
                else if (p2Point > p1Point)
                {
                    slotData.player2SlotGain += 10;
                    Debug.Log($"Player 2 wins Player 1's Court at slot {slotIndex + 1} and gains +10");
                }
                else
                {
                    Debug.Log($"Player 1's Court at slot {slotIndex + 1} ends in a tie");
                }
            }

            // Player 2 发起 Court
            if (player2HasCourt)
            {
                if (p2Point > p1Point)
                {
                    slotData.player2SlotGain += 10;
                    Debug.Log($"Player 2 wins Court at slot {slotIndex + 1} and gains +10");
                }
                else if (p1Point > p2Point)
                {
                    slotData.player1SlotGain += 10;
                    Debug.Log($"Player 1 wins Player 2's Court at slot {slotIndex + 1} and gains +10");
                }
                else
                {
                    Debug.Log($"Player 2's Court at slot {slotIndex + 1} ends in a tie");
                }
            }
        }

        // Step 6: 把这个 slot 的收益写入总 coins
        player1.coins += slotData.player1SlotGain;
        player2.coins += slotData.player2SlotGain;

        Debug.Log($"Slot {slotIndex + 1} result -> P1 +{slotData.player1SlotGain}, P2 +{slotData.player2SlotGain}");
    }
    /*private void ResolveSingleCardAtSlot(PlayerState player, int slotIndex)
    {
        if (player.timeline[slotIndex].IsEmpty)
        {
            return;
        }

        CardData card = player.timeline[slotIndex].currentCard.card;

        switch (card.effectType)
        {
            case CardEffectType.GainCoins:
                player.coins += 5;
                Debug.Log($"{player.playerId} gains +5 from slot {slotIndex + 1}");
                break;

            case CardEffectType.Lottery:
                if (HasUsableTimePointAtResolution(player, slotIndex))
                {
                    player.coins += 10;
                    Debug.Log($"{player.playerId} gains +10 from Lottery at slot {slotIndex + 1}");
                }
                else
                {
                    Debug.Log($"{player.playerId} Lottery failed at slot {slotIndex + 1}");
                }
                break;

            case CardEffectType.SetTimePoint:
            case CardEffectType.Barrier:
            case CardEffectType.None:
                // 这些暂时不直接加分
                break;

            case CardEffectType.Rob:
            case CardEffectType.Camera:
            case CardEffectType.Court:
            case CardEffectType.Joker:
                // 暂时未实现
                Debug.Log($"{player.playerId} has unresolved effect {card.effectType} at slot {slotIndex + 1}");
                break;
        }
    }*/

    private void ResolveEntireGame()
    {
        if (currentPhase != GamePhase.FinalResolution)
        {
            return;
        }

        Debug.Log("=== FINAL RESOLUTION START ===");

        player1.coins = 0;
        player2.coins = 0;

        player1.hasActiveCamera = false;
        player2.hasActiveCamera = false;

        ResetAllJokerCancellationFlags(player1);
        ResetAllJokerCancellationFlags(player2);
        
        for (int slotIndex = 0; slotIndex < maxRounds; slotIndex++)
        {
            ResolveSingleSlot(slotIndex);
        }

        currentPhase = GamePhase.GameEnded;

        Debug.Log($"Player 1 coins: {player1.coins}");
        Debug.Log($"Player 2 coins: {player2.coins}");

        if (player1.coins > player2.coins)
        {
            Debug.Log("Player 1 wins!");
        }
        else if (player2.coins > player1.coins)
        {
            Debug.Log("Player 2 wins!");
        }
        else
        {
            Debug.Log("Draw!");
        }

        RefreshAllUI();
    }
}