﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Logic : MonoBehaviour {

	private static Logic inst;
	private Grid grid;
	private UnitList unitList;
	private InfoPanel infoPanel;
	private Audio audio;

	private Combat combatManager;

	private Unit selectedUnit;
	private Tile selectedTile;

	private Player[] players;
	[SerializeField]
	private int currentPlayer = 0;
	[SerializeField]
	private int startingPlayer = 0;

	public Button endTurn;
	
	public GamePhase gamePhase = GamePhase.CompositionPhase;

	private void Awake() {
		if (!inst)
			inst = this;

		grid = GetComponentInChildren<Grid>();

		if (!grid)
			Debug.LogError("Grid does not exist!");

		unitList = GetComponent<UnitList>();
		if(!unitList)
			Debug.LogError("UnitList does not exist!");

		players = GetComponentsInChildren<Player>();
		if(players.Length == 0)
			Debug.LogError("No players present!");

		combatManager = GetComponent<Combat>();

		if (!combatManager)
			Debug.LogError("Combat Manager does not exist!");

		audio = GetComponent<Audio>();

		if (!audio)
			Debug.LogError("Audio manager does not exist!");

		foreach (Player player in players)
			player.StartPlacing();

		infoPanel = GetComponent<InfoPanel>();

		if (!infoPanel)
			Debug.LogError("InfoPanel does not exist!");

		infoPanel.Clear();
		infoPanel.UpdateTurnInfo(CurrentPlayer);
	}

	private void Update() {
		if (Input.GetMouseButtonUp(0)) {
			RaycastHit hit = MouseClick();
			if (hit.transform) {
				GameObject go = hit.transform.gameObject;

				Unit unit = go.GetComponent<Unit>();

				if (unit)
					UnitLClicked(unit);
				else {
					Tile tile = go.GetComponent<Tile>();

					if (tile)
						TileLClicked(tile);
				}

			}
		}

		if (Input.GetMouseButtonUp(1)) {
			RaycastHit hit = MouseClick();
			if (hit.transform) {
				GameObject go = hit.transform.gameObject;

				Unit unit = go.GetComponent<Unit>();

				if (unit)
					UnitRClicked(unit);
				else {
					Tile tile = go.GetComponent<Tile>();

					if (tile)
						TileRClicked(tile);
				}

			}
		}
	}

	private void UnitLClicked(Unit unit) {
		UnitSelected(unit);

		switch (gamePhase) {
			case GamePhase.PlacingPhase:

				break;

			case GamePhase.CombatPhase:

				break;
		}
	}

	private void TileLClicked(Tile tile) {
		TileSelected(tile);

		switch (gamePhase) {
			case GamePhase.PlacingPhase:

				break;

			case GamePhase.CombatPhase:

				break;
		}
	}

	private void UnitRClicked(Unit unit) {
		switch (gamePhase) {
			case GamePhase.PlacingPhase:
				if (CurrentPlayer.placementField.CoordsInRange(unit.Index))
					if (selectedUnit && selectedUnit.Owner == CurrentPlayer)
						if (grid.GetTile(unit.Index).IsPassable)
							SwapUnits(grid.GetTile(unit.Index));
				break;

			case GamePhase.CombatPhase:

				break;
		}
	}

	private void TileRClicked(Tile tile) {
		switch (gamePhase) {
			case GamePhase.PlacingPhase:
				if(CurrentPlayer.placementField.CoordsInRange(tile.Index))
					if (selectedUnit && selectedUnit.Owner == CurrentPlayer)
						if (tile.IsPassable)
							if (!tile.OccupyngUnit)
								selectedUnit.MoveTowardsTile(tile);
							else if (tile.OccupyngUnit.Owner == CurrentPlayer)
								SwapUnits(tile);
				break;

			case GamePhase.CombatPhase:

				break;
		}
	}

	private void SwapUnits(Tile tile) {
		Tile prevTile = grid.GetTile(selectedUnit.Index);
		Unit swap = tile.OccupyngUnit;
		swap.MoveTowardsTile(prevTile);
		selectedUnit.MoveTowardsTile(tile);
		prevTile.OccupyngUnit = swap;
	}

	public void SetupGameWorld(int[][] armies) {
		grid.GenerateGrid();		

		for (int i = 0; i < armies.Length; i++){
			List<Tile> tiles = players[i].PlacementField();
			for (int j = 0; j < armies[i].Length; j++)
				players[i].AddUnit((UnitType)armies[i][j], tiles[j], i);
		}

		SwtichGamePhase(GamePhase.PlacingPhase);
	}

	public void StartSetupPhase() {
		currentPlayer = startingPlayer = Random.Range(0, players.Length);

		infoPanel.Enabled(true);
		infoPanel.UpdateTurnInfo(CurrentPlayer);
	}

	public void StartCombatPhase() {
		Debug.Log("Start P: " + startingPlayer);
		Debug.Log("Current P: " + currentPlayer);

		currentPlayer = startingPlayer;

		Debug.Log("Current P: " + currentPlayer);
		infoPanel.UpdateTurnInfo(CurrentPlayer);
	}

	public void EndTurn() {
		switch (gamePhase) {
			case GamePhase.PlacingPhase:
				CurrentPlayer.HasFinishedPlacing = true;

				bool placingFinished = true;

				for (int i = 0; i < players.Length; i++)
					if (!players[i].HasFinishedPlacing)
						placingFinished = false;

				if (placingFinished)
					SwtichGamePhase(GamePhase.CombatPhase);

					break;

			case GamePhase.CombatPhase:
				break;
		}

		ChangePlayer();

		switch (gamePhase) {
			case GamePhase.PlacingPhase:
				break;

			case GamePhase.CombatPhase:
				CurrentPlayer.StartTurn();
				break;
		}

		ClearSelected();				
		infoPanel.UpdateTurnInfo(CurrentPlayer);
	}

	private void SwtichGamePhase(GamePhase phase) {
		gamePhase = phase;

		switch (phase) {
			case GamePhase.PlacingPhase:
				StartSetupPhase();
				break;

			case GamePhase.CombatPhase:
				StartCombatPhase();
				break;
		}
	}

	private void UnitCombat(Unit att, Unit def) {
		combatManager.ResolveCombat(att, def);
		if (def)
			combatManager.ResolveCombat(def, att);
	}

	private void ChangePlayer() {
		if (currentPlayer + 1 < players.Length)
			currentPlayer++;
		else
			currentPlayer = 0;
	}
	
	private void ClearSelected() {
		if(selectedUnit)
			selectedUnit = null;
		if(selectedTile)
			selectedTile = null;

		infoPanel.Clear();
	}

	private void UnitSelected(Unit unit) {
		selectedUnit = unit;
		infoPanel.UpdateUnitInfo(unit);
	}

	private void TileSelected(Tile tile) {
		selectedTile = tile;
		infoPanel.UpdateTileInfo(tile);
	}

	private RaycastHit MouseClick() {
		RaycastHit hit; 
		Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit);

		return hit;
	}

	#region Getters and Setters 	
	public static Logic Inst {
		get { return inst; }
	}

	public Player CurrentPlayer {
		get { return players[currentPlayer]; }
	}

	public Grid Grid {
		get { return grid; }
	}

	public UnitList UnitList {
		get { return unitList; }
	}

	public InfoPanel InfoPanel {
		get { return infoPanel; }
	}

	public Player[] Players {
		get { return players; }
	}
	#endregion
}