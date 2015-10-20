﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class Unit : MonoBehaviour {

	public UnitType type;
	private CubeIndex index;

	public int movePoints;
	public int maxHitpoints;
	public int attack;
	public int defense;
	public int cost;

	[Range(0,100)]
	public int hitChance;
	[Range(0,100)]
	public int dodgeChance;
	public int attackRange;

	private int attackModifer = 0;
	private int defenseModifer = 0;
	private int hitModifier = 0;
	private int dodgeModifier = 0;
	private int currentHP;
	private int currentMP;

	private bool canMove = true;

	private Player owner;

	public List<Buff> currentBuffs = new List<Buff>();	//Every effect this unit is currently under
	private Queue<Tile> currentPath;

	private List<Tile> highlighted = new List<Tile>();

	private Animator unitAnim;

	public List<TextSpawn> buffsToSpawn = new List<TextSpawn>();
	private float buffDelay = 1f;

	private void Start() {
		currentHP = maxHitpoints;
		currentMP = movePoints;

		try{	//Not all units have animations right now. Otherwise, not necessary.
			unitAnim = gameObject.transform.GetChild(0).GetComponent<Animator>();
		}
		catch(Exception e){
			//Nothing to do here...
		}
	}

//	private void OnMouseEnter() {
//		Logic.Inst.InfoPanel.UpdateToolTip(this);
//	}
//
//	private void OnMouseExit() {
//		Logic.Inst.InfoPanel.UpdateToolTip(null);
//	}

	public void MoveTowardsTile(Tile tile) {
		Logic.Inst.Audio.PlaySFX(SFX.Unit_Move);

		Logic.Inst.Grid.TileAt(index).OccupyingUnit = null;

		if (Logic.Inst.gamePhase == GamePhase.CombatPhase){
			currentPath = new Queue<Tile>(Logic.Inst.Path.GetPath(Logic.Inst.Grid.TileAt(index), tile));
			currentPath = new Queue<Tile>(currentPath.Reverse());
			StartCoroutine(Move());
		}		
		else {
			transform.position = tile.transform.position;
		}

		index = tile.Index;
		tile.OccupyingUnit = this;
		//Handles adjacency buffs
		AdjacencyCheck();

		//Handles persistent passives
		PersistentAoECheck();

		Altar altar = Logic.Inst.GetAltar(tile.Index);

		if (altar)
			altar.PlayerCaptureAltar(owner);
		ClearHighlightedTiles();
	}

	private IEnumerator Move(){
		if(unitAnim){
			unitAnim.SetBool("Moving", true);
		}

		while(currentPath.Count != 0){
			if(unitAnim){	//These checks are purely here until we have animation for all the units.
				unitAnim.SetBool("Moving", true);
			}

			Tile tile = currentPath.Dequeue();
			Quaternion dir = Quaternion.LookRotation(tile.transform.position - transform.position);
			while(transform.rotation != dir){
				transform.rotation = Quaternion.RotateTowards(transform.rotation, dir, Time.deltaTime * 2f * 360f/Mathf.PI);
				yield return new WaitForEndOfFrame();
			}

			while(transform.position != tile.transform.position){
				transform.position = Vector3.MoveTowards(transform.position, tile.transform.position, Time.deltaTime * 2f);
				yield return new WaitForEndOfFrame();	
			}
			transform.position = tile.transform.position;
			currentMP -= tile.MoveCost;

//			foreach (Buff bff in currentBuffs){
//				if(bff.buffType == BuffType.HexTerrain){
//					if((bff.isBio && bff.bioType == tile.Biome) || (!bff.isBio&& bff.terType == tile.Terrain)){
//						bff.ChangeValue(this,true);
//					}
//				}
//			}
			if(unitAnim){
				unitAnim.SetBool("Moving", false);
			}

			yield return new WaitForSeconds(0.25f);
		}
		if(Logic.Inst.CurrentPlayer == Owner && !Logic.Inst.SelectedUnit){
			Logic.Inst.UnitSelected(this);
			Logic.Inst.HighlightMoveRange(this);
		}

		if(unitAnim){
			unitAnim.SetBool("Moving", false);
		}

		yield return null;
	}

	public void UnitSelected(){
		List<Tile> ret = Logic.Inst.Grid.TilesInRange(index, currentMP);

		foreach(Tile tile in ret){
			if(tile.OccupyingUnit != null){
				if(tile.OccupyingUnit.Owner != Owner && canMove)
					if(InAttackRange(tile.OccupyingUnit)){
						tile.LineColour(Color.red);
						tile.LineWidth(0.1f);
						highlighted.Add(tile);
					}
				else if(InMoveRange(tile)){
					tile.LineColour(Color.grey);
					tile.LineWidth(0.1f);
					highlighted.Add(tile);
				}
			}
			else if(InMoveRange(tile)){
				tile.LineColour(Color.green);
				tile.LineWidth(0.1f);
				highlighted.Add(tile);
			}
		}

		Logic.Inst.Grid.TileAt(index).LineColour(Color.cyan);
		Logic.Inst.Grid.TileAt(index).LineWidth(0.1f);
		highlighted.Add(Logic.Inst.Grid.TileAt(index));
	}

	public void ClearHighlightedTiles() {
		foreach(Tile tile in highlighted){
			tile.LineColour(Color.black);
			tile.LineWidth(0.03f);			
		}

		highlighted.Clear();
	}

	public void UnitKilled() {
		owner.RemoveUnit(this);

		if(owner.army.Count <= 0){
			if(owner == Logic.Inst.Players[0]){
				print ("PLAYER 1 DEFEATED");
				Logic.Inst.winningPlayer = 1;
				Logic.Inst.PlayerEliminated(0);				
			}
			else{
				print ("PLAYER 2 DEFEATED");
				Logic.Inst.winningPlayer = 0;
				Logic.Inst.PlayerEliminated(1);
			}
		}

		if(type == UnitType.Hero && owner.hero.passive.passive == PassiveType.PersitentAoE){
			foreach(Unit unit in owner.army){
				RemovePassiveBuff(unit, true);
			}
		}

		Debug.Log(type + " was killed");
		DestroyImmediate(this.gameObject);
		Logic.Inst.Audio.PlaySFX(SFX.Unit_Death);
	}

	public void UnitSacrificed() {
		owner.Faith += Logic.Inst.faithPtsPerSacrifice;
		owner.RemoveUnit(this);
		Debug.Log(type + " was sacrificed");
		DestroyImmediate(this.gameObject);
		Logic.Inst.Audio.PlaySFX(SFX.Unit_Death);
	}

	public bool InAttackRange(Unit unit) {
		return Logic.Inst.Grid.TilesInRange(index, attackRange).Contains(Logic.Inst.Grid.TileAt(unit.Index));
	}

	public bool InMoveRange(Tile tile) {
		List<Tile> ret = Logic.Inst.Grid.TilesInRange(index, currentMP);

		if(currentMP > 0)
			ret.RemoveAll(item => {
				int pathCost = Logic.Inst.Path.PathCost(Logic.Inst.Path.GetPath(Logic.Inst.Grid.TileAt(index), item));
				return (pathCost > currentMP || pathCost <= 0);
			});
		else
			ret.Clear();
		return ret.Contains(tile);
	}

	public void SetUnitMaterial(){
		if(gameObject.transform.childCount > 0){
			Transform model = gameObject.transform.GetChild(0);
			foreach(Transform child in model){
				if(child.name == "body"){
					try{
						child.GetComponent<SkinnedMeshRenderer>().materials[1].color = Owner.playerColour;
					}
					catch(Exception e){
						child.GetComponent<MeshRenderer>().materials[1].color = Owner.playerColour;
					}
				}
			}
		}
	}

	public void OnTurnStart(){
		GameObject tempText;
		List<int> finishedBuffs = new List<int>();

		currentMP = movePoints;

		foreach (Buff bff in currentBuffs){
			bff.duration--;
			if(bff.duration == 0 &&  !bff.permanent){				//If the effect is done
				bff.ChangeValue(this,false);	//Alter this units relative stat. False indicates that the effect is being removed.
//				buffsToSpawn.Add(new TextSpawn(bff,this,false));
//				SpawnBuffText(bff,this,false);
				finishedBuffs.Add(bff.ID);
			}
		}

//		SpawnBuffText(buffsToSpawn);

		RemoveBuffs(this,finishedBuffs);

		CalculateModifiers();
	}

	public void AddBuff(Buff bff){
		Buff nEft = null;
		bool newBuff = true;
		GameObject tempText;

		foreach(Buff buff in currentBuffs){
			if(buff.ID == bff.ID){
				newBuff = false;
				buff.duration = bff.duration;
			}
		}

		if(!bff.oneShot && newBuff){
			nEft = new Buff(bff.ID,bff.buffType,bff.duration,bff.effectType,bff.strength,bff.wrath,bff.targetType,bff.permanent,bff.procced,bff.oneShot,bff.adjType,bff.adjUnits,bff.isBio,bff.terType,bff.bioType);
			currentBuffs.Add(nEft);
		}

		if(bff.buffType == BuffType.Stat && newBuff){
			bff.ChangeValue(this,true);
		}

		if (newBuff) {
//			SpawnBuffText (nEft,this,true);
			buffsToSpawn.Add(new TextSpawn(nEft,this,true));
		}
	}

	public void AdjacencyCheck(){
		foreach(Buff buff in currentBuffs){
			List<Tile> inRange = new List<Tile>();
			inRange = Logic.Inst.Grid.TilesInRange(index,1);
			
			if(buff.buffType == BuffType.Adjacent){
				CalculateModifiers();
			}
			
			foreach(Tile adjTile in inRange){
				if(adjTile.OccupyingUnit){
					foreach(Buff adjBuff in adjTile.OccupyingUnit.currentBuffs){
						if(adjBuff.buffType == BuffType.Adjacent){
							adjTile.OccupyingUnit.CalculateModifiers();
						}
					}
				}
			}
		}
	}

	public void CalculateModifiers(){
		attackModifer = 0;
		defenseModifer = 0;
		hitModifier = 0;
		dodgeModifier = 0;
		
		CalcAdjacency();

		foreach (Buff bff in currentBuffs){
			if((bff.duration > 0 || bff.permanent) && bff.buffType == BuffType.Stat){
				bff.ChangeValue(this,true);
				if(!bff.procced){
					buffsToSpawn.Add(new TextSpawn(bff,this,true));
//					SpawnBuffText(bff,this,true);
				}
			}
		}

		StartCoroutine("SpawnBuffText",buffsToSpawn);
	}

	public void CalcAdjacency(){
		List<Tile> inRange = new List<Tile>();
		int proced = 0;

		foreach(Buff buff in currentBuffs){
			proced = 0;
			if(buff.buffType == BuffType.Adjacent){
				inRange = Logic.Inst.Grid.TilesInRange(index,1);
				foreach(Tile tile in inRange){
					if(tile.OccupyingUnit){
						switch(buff.adjType){
						case AdjacencyType.Friends:
							if(buff.adjUnits.Contains(tile.OccupyingUnit.type) && tile.OccupyingUnit.owner == owner){
								buff.ChangeValue(this,true);
								buffsToSpawn.Add(new TextSpawn(buff,this,true));
//								SpawnBuffText(buff,this,true);
								proced++;
							}
							break;

						case AdjacencyType.Enemies:
							if(buff.adjUnits.Contains(tile.OccupyingUnit.type) && tile.OccupyingUnit.owner != owner){
								buff.ChangeValue(this,true);
								buffsToSpawn.Add(new TextSpawn(buff,this,true));
//								SpawnBuffText(buff,this,true);
								proced++;
							}
							break;

						case AdjacencyType.Both:
							if(buff.adjUnits.Contains(tile.OccupyingUnit.type)){
								buff.ChangeValue(this,true);
								buffsToSpawn.Add(new TextSpawn(buff,this,true));
//								SpawnBuffText(buff,this,true);
								proced++;
							}
							break;

						default:
							break;
						}
					}
				}

				if(proced > 0){
					buff.ChangeValue(this,false); //To account for the fact that the unit will count itself.
				}

				if(proced <= 1){
					buffsToSpawn.Add(new TextSpawn(buff,this,false));
//					SpawnBuffText(buff,this,false);
				}
			}
		}

		StartCoroutine("SpawnBuffText",buffsToSpawn);
	}

	public void PersistentAoECheck(){
		List<Tile> inRange = new List<Tile>();

		inRange = Logic.Inst.Grid.TilesInRange(index,owner.hero.passive.AoERange);

		if(type == UnitType.Hero && owner.hero.passive.passive == PassiveType.PersitentAoE){
			foreach(Unit unit in owner.army){
				if(!inRange.Contains(Logic.Inst.Grid.TileAt(unit.index))){
					RemovePassiveBuff(unit,true);
					buffDelay = 0;
				}
			}
			
			owner.hero.passive.ApplyBuffAoE(index);
		}
		else if(owner.hero.passive.passive == PassiveType.PersitentAoE){
			if(inRange.Contains(Logic.Inst.Grid.TileAt(owner.hero.gameObject.GetComponent<Unit>().index))){
				owner.hero.passive.ApplyBuffSingle(index);
			}
			else{
				RemovePassiveBuff(this, true);
			}
		}
	}

	private void RemovePassiveBuff(Unit unit, bool makeText){
		int buffToRemove = -1;

		foreach(Buff buff in unit.currentBuffs){
			if(buffToRemove < 0){
				foreach(Buff passBuff in unit.owner.hero.passive.buffs){
					if(buff.ID == passBuff.ID && unit.owner.hero.passive.affected.Contains(unit.type)){
						buffToRemove = unit.currentBuffs.IndexOf(buff);
					}
				}
			}
		}
		if(buffToRemove > -1){
//			if(makeText){
				buffsToSpawn.Add(new TextSpawn(unit.currentBuffs[buffToRemove],unit,false));
//			}
			unit.currentBuffs[buffToRemove].ChangeValue(unit,false);
			unit.currentBuffs.RemoveAt(buffToRemove);
			buffToRemove = -1;
		}

		StartCoroutine("SpawnBuffText",buffsToSpawn);
	}

	private void RemoveBuffs(Unit unit, List<int> buffID){
		int buffToRemove = -1;

		for(int i = 0; i < buffID.Count; i++){
			foreach(Buff buff in unit.currentBuffs){
				if(buff.ID == buffID[i]){
					buffToRemove = unit.currentBuffs.IndexOf(buff);
				}
			}
			if(buffToRemove > -1){
				buffsToSpawn.Add(new TextSpawn(unit.currentBuffs[buffToRemove],unit,false));
				unit.currentBuffs.RemoveAt(buffToRemove);
				buffToRemove = -1;
				print(buffsToSpawn[i].buff.ID);
			}
		}

		StartCoroutine("SpawnBuffText",buffsToSpawn);
	}
	
	public IEnumerator SpawnBuffText(List<TextSpawn> buffList){
		GameObject tempText = null;
		int multiplier = 1;
		string operatorString;

		if(buffList.Count < 2){
			buffDelay = 0f;
		}

		for(int i = 0; i < buffList.Count; i++){
			if (buffList[i].buff.buffType != BuffType.Adjacent) {
				if (buffList[i].add) {
					multiplier = 1;
					operatorString = "+ ";
				} 
				else {
					multiplier = -1;
					operatorString = "";
				}

				if (buffList[i].buff.effectType != EffectType.Damage && buffList[i].buff.effectType != EffectType.Health) {
					if((!buffList[i].add && buffList[i].buff.procced) || (buffList[i].add && !buffList[i].buff.procced)){
						tempText = MonoBehaviour.Instantiate (Logic.Inst.buffText, (buffList[i].unit.gameObject.transform.position + Vector3.up * Logic.Inst.offsetDist), Quaternion.identity) as GameObject;
						tempText.GetComponent<TextMesh> ().text = operatorString + (buffList[i].buff.strength * multiplier) + " " + buffList[i].buff.effectType;
					}
				}

				if (!buffList[i].add) {
					buffList[i].buff.procced = false;
				} 
				else {
					buffList[i].buff.procced = true;
				}
			}

			yield return new WaitForSeconds(buffDelay);
		}

		buffsToSpawn.Clear();
		buffDelay = 1f;
	}
		
	#region Getters and Setters
	public CubeIndex Index {
		get { return index; }
		set { index = value; }
	}

	public int CurrentHitpoints {
		get { return currentHP; }
		set { currentHP = value; }
	}

	public int AttackModifier {
		get { return attackModifer; }
		set { attackModifer = value; }
	}

	public int DefenseModifier {
		get { return defenseModifer; }
		set { defenseModifer= value; }
	}

	public int HitModifier {
		get { return hitModifier; }
		set { hitModifier = value; }
	}

	public int DodgeModifier {
		get { return dodgeModifier; }
		set { dodgeModifier = value; }
	}

	public int TotalAttack {
		get { return attack + attackModifer; }
	}

	public int TotalDefense {
		get { return defense + defenseModifer; }
	}

	public int TotalHitChance {
		get { return hitChance + hitModifier; }
	}

	public int TotalDodgeChance {
		get { return dodgeChance + dodgeModifier; }
	}

	public Player Owner {
		get { return owner; }
		set { owner = value; }
	}

	public bool CanMove {
		get { return canMove; }
		set { canMove = value; }
	}

	public int CurrentMovePoints {
		get { return currentMP; }
		set { currentMP = value; }
	}
	#endregion
}