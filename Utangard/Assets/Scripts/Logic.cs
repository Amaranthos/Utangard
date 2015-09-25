﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class Logic : MonoBehaviour {

	private static Logic inst;
	private Grid grid;
	private UnitList unitList;
	private HeroList heroList;
	private TerrainList terrainList;
	private InfoPanel infoPanel;
	private Audio _audio;
	private Path path;

	private Combat combatManager;

	private Unit selectedUnit;
	private Tile selectedTile;

	private Player[] players;
	private int currentPlayer = -1;
	private int startingPlayer = -1;
	private int winningPlayer = -1;
	private int turnsRemaining;

	private List<Tile> highlightedTiles = new List<Tile>();

	private List<Altar> altars = new List<Altar>();

	public Button endTurn;
	public Button sacrifice;
	
	public GamePhase gamePhase = GamePhase.ArmySelectPhase;

	public int numAltars;
	public int faithPtsPerAltar;
	public int faithPtsPerSacrifice;

	public int turnsForVictory;

	private void Awake() {
		if (!inst)
			inst = this;

		grid = GetComponentInChildren<Grid>();

		if (!grid)
			Debug.LogError("Grid does not exist!");

		unitList = GetComponent<UnitList>();
		if(!unitList)
			Debug.LogError("UnitList does not exist!");

		heroList = GetComponent<HeroList>();
		if (!heroList)
			Debug.LogError("HeroList does not exist!");

		terrainList = GetComponent<TerrainList>();
		if (!terrainList)
			Debug.LogError("TerrainList does not exist!");

		players = GetComponentsInChildren<Player>();
		if(players.Length == 0)
			Debug.LogError("No players present!");

		combatManager = GetComponent<Combat>();

		if (!combatManager)
			Debug.LogError("Combat Manager does not exist!");

		_audio = GetComponent<Audio>();

		if (!_audio)
			Debug.LogError("Audio manager does not exist!");

		foreach (Player player in players)
			player.StartPlacing();

		infoPanel = GetComponent<InfoPanel>();

		if (!infoPanel)
			Debug.LogError("InfoPanel does not exist!");

		path = GetComponent<Path>();

		if (!path)
			Debug.LogError("Pathfinder does not exist!");

		infoPanel.Clear();
	}

	private void Update() {
		if(gamePhase == GamePhase.CombatPhase){
			if(selectedUnit && selectedUnit.CanMove){
				Altar altar = GetAltar(selectedUnit.Index);
				if(altar){
					sacrifice.interactable = true;
				}
			}
			else {
				sacrifice.interactable = false;
			}
		}

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
					else {
						Altar altar = go.GetComponent<Altar>();

						if (altar)
							TileLClicked(grid.GetTile(altar.Index));
					}
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
					else {
						Altar altar = go.GetComponent<Altar>();

						if (altar)
							TileRClicked(grid.GetTile(altar.Index));
					}
				}

			}

			if(gamePhase == GamePhase.TargetPhase){	//So players can back out of an ability cast;
				gamePhase = GamePhase.CombatPhase;
				print ("TARGETING ABORTED");
				ClearHighlightedTiles();
				UnitLClicked(heroList.heroes[currentPlayer].hero);
			}
		}
	}

	private void UnitLClicked(Unit unit) {
		UnitSelected(unit);

		switch (gamePhase) {
			case GamePhase.PlacingPhase:
			_audio.PlaySFX(SFX.Unit_Click);
				break;

			case GamePhase.CombatPhase:
				if(unit.CanMove && unit.Owner == CurrentPlayer){
					HighlightMoveRange(unit);
					_audio.PlaySFX(SFX.Unit_Click);
				}
				break;

			case GamePhase.TargetPhase:		//This is horribly ineffecitent. Will likely have to store a record of each hero in logic once selected.
				Hero hero;
				foreach (Unit unt in players[currentPlayer].army){
					if(unt.type == UnitType.Hero){
						hero = unt.GetComponent<Hero>();
						if(hero.currentStage == AbilityStage.GetUnit){
							hero.ReceiveTarget(unit,grid.GetTile(unit.Index));
							print("FOUND TARGET!");
						}
					}
				}
				print("TARGETING COMPLETE!");
				break;
		}
	}

	private void TileLClicked(Tile tile) {
		TileSelected(tile);

		switch (gamePhase) {
			case GamePhase.PlacingPhase:
			_audio.PlaySFX(SFX.Scroll);
				break;

			case GamePhase.CombatPhase:
			_audio.PlaySFX(SFX.Scroll);
				break;

			case GamePhase.TargetPhase:
				Hero hero;
				foreach (Unit unit in players[currentPlayer].army){
					if(unit.type == UnitType.Hero){
						hero = unit.GetComponent<Hero>();
						if(hero.currentStage == AbilityStage.GetUnit){
							hero.ReceiveTarget(tile.OccupyngUnit,tile);
							print("FOUND TARGET!");
						}

						else{
							hero.ReceiveTarget(unit,tile);
							print ("FOUND LOCATION");
						}
					}
				}
				print("TARGETING COMPLETE!");
				break;
		}
	}

	private void UnitRClicked(Unit unit) {
		switch (gamePhase) {
			case GamePhase.PlacingPhase:
				if (CurrentPlayer.placementBoundaries.CoordsInRange(unit.Index))
					if (selectedUnit && selectedUnit.Owner == CurrentPlayer)
						if (grid.GetTile(unit.Index).IsPassable){
							SwapUnits(grid.GetTile(unit.Index));
							_audio.PlaySFX(SFX.Unit_Move);
							}

				break;

			case GamePhase.CombatPhase:
				if (selectedUnit && selectedUnit.Owner == CurrentPlayer && selectedUnit.CanMove)
					if (unit.Owner != CurrentPlayer)
						if (selectedUnit.InAttackRange(unit)){
							UnitCombat(selectedUnit, unit);
							_audio.PlaySFX(SFX.Rune_Roll);
						}
				break;
		}
	}

	private void TileRClicked(Tile tile) {
		switch (gamePhase) {
			case GamePhase.PlacingPhase:
				if(CurrentPlayer.placementBoundaries.CoordsInRange(tile.Index)){
					if (selectedUnit && selectedUnit.Owner == CurrentPlayer && selectedUnit.CanMove)
						if (tile.IsPassable)
							if (!tile.OccupyngUnit)
								selectedUnit.MoveTowardsTile(tile);
							else if (tile.OccupyngUnit.Owner == CurrentPlayer)
								SwapUnits(tile);
				}
				else
				_audio.PlaySFX(SFX.Unit_CantMoveThere);
				break;

			case GamePhase.CombatPhase:
				if (selectedUnit && selectedUnit.Owner == CurrentPlayer && selectedUnit.CanMove){
					if (selectedUnit.InMoveRange(tile))
					{
						if (!tile.OccupyngUnit) {
							selectedUnit.MoveTowardsTile(tile);
							HighlightMoveRange(selectedUnit);
						}
						else if (tile.OccupyngUnit.Owner != CurrentPlayer)
							UnitCombat(selectedUnit, tile.OccupyngUnit);
					}
					else
						_audio.PlaySFX(SFX.Unit_CantMoveThere);
				}
				break;
		}
	}

	private void SwapUnits(Tile tile) {
		_audio.PlaySFX(SFX.Unit_Move);
		Tile prevTile = grid.GetTile(selectedUnit.Index);
		Unit swap = tile.OccupyngUnit;
		swap.MoveTowardsTile(prevTile);
		selectedUnit.MoveTowardsTile(tile);
		prevTile.OccupyngUnit = swap;
	}

	public void SetupGameWorld(int[][] armies) {
		GUIManager.inst.AssignTextures();
		grid.GenerateGrid();

		for (int i = 0; i < armies.Length; i++){
			List<Tile> tiles = players[i].PlacementField();

			for (int j = 0; j < armies[i].Length; j++)
				players[i].SpawnUnit((UnitType)armies[i][j], tiles[j], i);

			players[i].SpawnHero(tiles[armies[i].Length], i);
		}

		for (int i = 0; i < numAltars; i++) {
			Tile rand = Grid.GetTile(Random.Range(0, Grid.gridSize.x), Random.Range(0, Grid.gridSize.y));
			Altar altar =  ((GameObject)Instantiate(terrainList.GetAltar(), rand.transform.position, Quaternion.Euler(Vector3.up * i * 45))).GetComponent<Altar>();
			altar.Index = rand.Index;
			altars.Add(altar);
			altar.PlayerCaptureAltar(players[Random.Range(0, players.Length)]);
		}

		SwitchGamePhase(GamePhase.PlacingPhase);
	}

	public void StartSetupPhase() {
		GUIManager.inst.GUICanvas.SetActive(true);

		currentPlayer = startingPlayer = Random.Range(0, players.Length);
		GUIManager.inst.UpdatePlayerGUI(currentPlayer);

		ChangeTileOutlines(CurrentPlayer.PlacementField(), CurrentPlayer.playerColour, 0.06f);

		Camera.main.GetComponent<Vision>().enabled = true;

		infoPanel.Enabled(true);
		infoPanel.UpdateTurnInfo(CurrentPlayer);
	}

	public void StartCombatPhase() {
		currentPlayer = startingPlayer;
		GUIManager.inst.UpdatePlayerGUI(currentPlayer);
		infoPanel.UpdateTurnInfo(CurrentPlayer);
	}

	private void ChangeTileOutlines(List<Tile> tiles, Color colour, float thickness) {
		for (int i = 0; i < tiles.Count; i++) {
			if (tiles[i]) {
				tiles[i].SetLineColour(colour);
				tiles[i].SetWidth(thickness);
			}
		}
	}

	public void ClearHighlightedTiles() {
		ChangeTileOutlines(highlightedTiles, Color.black, 0.03f);
	}

	private void HighlightMoveRange(Unit unit) {
		ClearHighlightedTiles();

		highlightedTiles = grid.TilesInRange(unit.Index, unit.movePoints);
		ChangeTileOutlines(highlightedTiles, Color.green, 0.06f);
	}

	public void HighlightAbilityRange (Ability ability, Unit unit){
		ClearHighlightedTiles();

		highlightedTiles = grid.TilesInRange(unit.Index, ability.range);
		ChangeTileOutlines(highlightedTiles, Color.yellow, 0.06f);
	}

	public bool PlayesPositionedUnits() {
		bool placingFinished = true;

		for (int i = 0; i < players.Length; i++)
			if (!players[i].HasFinishedPlacing)
				placingFinished = false;

		return placingFinished;
	}

	public void EndTurn() {
		Player prevPlayer = CurrentPlayer;
		ChangePlayer();

		ClearSelected();
		infoPanel.UpdateTurnInfo(CurrentPlayer);
		GUIManager.inst.UpdatePlayerGUI(currentPlayer);

		switch (gamePhase) {
			case GamePhase.PlacingPhase:
				prevPlayer.HasFinishedPlacing = true;
				ChangeTileOutlines(prevPlayer.PlacementField(), Color.black, 0.03f);

				if (PlayesPositionedUnits()) {
					SwitchGamePhase(GamePhase.CombatPhase);
					return;
				}

				ChangeTileOutlines(CurrentPlayer.PlacementField(), CurrentPlayer.playerColour, 0.06f);
				break;

			case GamePhase.CombatPhase:
				CheckIfPlayerWinning();

				CurrentPlayer.StartTurn();
				AddFaithPerAltar();
				break;
		}
	}

	private void CheckIfPlayerWinning() {
		int winning = -1;
		for (int i = 0; i < players.Length; i++)
			if (players[i].capturedAltars.Count == numAltars)
				winning = i;

		if (winning != -1) {
			if (winning == winningPlayer) {
				turnsRemaining -= 1;

				if (turnsRemaining <= 0)
					EndGame();
			}
			else {
				winningPlayer = winning;
				turnsRemaining = turnsForVictory;
			}
		}
		else {
			for (int i = 0; i < players.Length; i++)
				if (players[i].army.Count <= 0)
					PlayerEliminated(i);
		}
	}

	private void PlayerEliminated(int player) {
		players[player].Defeated = true;

		int countAlive = 0;

		for (int i = 0; i < players.Length; i++)
			if (!players[i].Defeated)
				countAlive++;

		if (countAlive <= 1)
			EndGame();
	}

	private void EndGame() {
		gamePhase = GamePhase.FinishedPhase;
	}

	private void SwitchGamePhase(GamePhase phase) {
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
		ClearSelected();
		att.CanMove = false;
		combatManager.ResolveCombat(att, def);
		if (def)
			combatManager.ResolveCombat(def, att);
	}

	private void ChangePlayer() {
		if (currentPlayer + 1 < players.Length)
			currentPlayer++;
		else
			currentPlayer = 0;

		if (CurrentPlayer.Defeated)
			ChangePlayer();
	}
	
	private void ClearSelected() {
		if(selectedUnit)
			selectedUnit = null;
		if(selectedTile)
			selectedTile = null;

		ClearHighlightedTiles();

		infoPanel.Clear();
	}

	private void UnitSelected(Unit unit) {
		if(gamePhase == GamePhase.CombatPhase){
			ClearHighlightedTiles();
		}
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

	private void AddFaithPerAltar() {
		for(int j = 0; j < CurrentPlayer.capturedAltars.Count; j++){
			CurrentPlayer.Faith += faithPtsPerAltar;
		}
	}

	public void SacrificeUnit() {
		selectedUnit.UnitSacrificed();
	}

	#region Getters and Setters 	
	public Altar GetAltar(PairInt Index) {
		return altars.Find(item=>item.Index==Index);
	}

	public static Logic Inst {
		get { return inst; }
	}

	public Player CurrentPlayer {
		get { return players[currentPlayer]; }
	}

	// I added this because I wanted to know the number of the current player - Callan
	public int CurrentPlayerNum {
		get { return currentPlayer; }
	}

	public Audio Audio{
		get { return _audio; }
	}

	public Grid Grid {
		get { return grid; }
	}

	public UnitList UnitList {
		get { return unitList; }
	}

	public HeroList HeroList {
		get { return heroList; }
	}

	public InfoPanel InfoPanel {
		get { return infoPanel; }
	}

	public Player[] Players {
		get { return players; }
	}

	public Path Path {
		get { return path; }
	}
	#endregion
}