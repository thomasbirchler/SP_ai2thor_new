﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SimObjPhysics : MonoBehaviour, SimpleSimObj
{
	[Header("String ID of this Object")]
	[SerializeField]
	public string uniqueID = string.Empty;

	[Header("Object Type")]
	[SerializeField]
	public SimObjType Type = SimObjType.Undefined;

	[Header("Primary Property (Must Have only 1)")]
	[SerializeField]
	public SimObjPrimaryProperty PrimaryProperty;

	[Header("Additional Properties (Can Have Multiple)")]
	[SerializeField]
	public SimObjSecondaryProperty[] SecondaryProperties;

	[Header("non Axis-Aligned Box enclosing all colliders of this object")]
	//This can be used to get the "bounds" of the object, but needs to be created manually
	//we should look into a programatic way to figure this out so we don't have to set it up for EVERY object
	//for now, only CanPickup objects have a BoundingBox, although maybe every sim object needs one for
	//spawning eventually? For now make sure the Box Collider component is disabled on this, because it's literally
	//just holding values for the center and size of the box.
	public GameObject BoundingBox = null;

	[Header("Raycast to these points to determine Visible/Interactable")]
	[SerializeField]
	public Transform[] VisibilityPoints = null;

    [Header("If this object is a Receptacle, put all trigger boxes here")]
	[SerializeField]
	public GameObject[] ReceptacleTriggerBoxes = null;

	[Header("State information Bools here")]
	public bool isVisible = false;
	public bool isInteractable = false;
	public bool isColliding = false;

	private Bounds bounds;

	[Header("Non - Trigger Colliders of this object")]
	public Collider[] MyColliders = null;

	[Header("High Friction physics values")]
	public float HFdynamicfriction;
	public float HFstaticfriction;
	public float HFbounciness;
	public float HFrbdrag;
	public float HFrbangulardrag;

	private float RBoriginalDrag;
	private float RBoriginalAngularDrag;

	private PhysicsMaterialValues[] OriginalPhysicsMaterialValuesForAllMyColliders = null;

	public Dictionary<Collider, ContactPoint[]> contactPointsDictionary = new Dictionary<Collider, ContactPoint[]>();

	//if this object is a receptacle, get all valid spawn points from any child ReceptacleTriggerBoxes and sort them by distance to Agent
	List<ReceptacleSpawnPoint> MySpawnPoints = new List<ReceptacleSpawnPoint>();

	//initial position object spawned in in case we want to reset the scene
	//private Vector3 startPosition;   

	public class PhysicsMaterialValues
	{
		public float DynamicFriction;
		public float StaticFriction;
		public float Bounciness;

		public PhysicsMaterialValues (float dFriction, float sFriction, float b)
		{
			DynamicFriction = dFriction;
			StaticFriction = sFriction;
			Bounciness = b;
		}
	}

	public string UniqueID
	{
		get
		{
			return uniqueID;
		}

		set
		{
			uniqueID = value;
		}
	}

       public bool IsVisible
       {
               get
               {
                       return isVisible;
               }

               set {
                       isVisible = value;
               }
       }

	public Bounds Bounds
	{
		get
		{
			// XXX must define how to get the bounds of the simobj
			//We can use Bounds.Encapsulate to grow a bounding box to enclose all Visibility Points on this Sim Obj
			return bounds;
		}
	}


	public bool IsPickupable
	{
		get
		{
			return this.PrimaryProperty == SimObjPrimaryProperty.CanPickup;
		}
	}


	public SimObjType ObjType
	{
		get
		{
			return Type;
		}
	}

	public int ReceptacleCount
	{
		get
		{
			return 0;
		}
	}

	//get all the UniqueID strings of all objects contained by this receptacle object
	public List<string> ReceptacleObjectIds
	{
		get
		{
			return this.Contains();
		}
	}

	//get all objects contained by this receptacle object as a list of SimObjPhysics
	public List<SimObjPhysics> ReceptacleObjects
	{
		get
		{
			return this.ContainsGameObject();
		}

	}

	public List<PivotSimObj> PivotSimObjs
	{
		get
		{
			return new List<PivotSimObj>();
		}
	}

	public bool Open()
	{
		// XXX need to implement
		return false;
	}

	public bool Close()
	{
		// XXX need to implement
		return false;
	}

	public bool IsToggleable
	{
		get { return this.GetComponent<CanToggleOnOff>(); }
	}

	public bool IsOpenable
	{
		get { return this.GetComponent<CanOpen_Object>(); }
	}

	public bool IsToggled
	{
		get
		{
			CanToggleOnOff ctoo = this.GetComponent<CanToggleOnOff>();

			if (ctoo != null)
			{
				return ctoo.isOn;
			}
			else
			{
				return false;
			}
		}
	}

	public bool IsOpen
	{
		get
		{
			CanOpen_Object coo = this.GetComponent<CanOpen_Object>();

			if (coo != null)
			{
				return coo.isOpen;
			}
			else
			{
				return false;
			}
		}
	}

	public bool IsReceptacle
	{
		get
		{
			return Array.IndexOf(SecondaryProperties, SimObjSecondaryProperty.Receptacle) > -1 &&
			 ReceptacleTriggerBoxes != null;
		}
	}
	
	private void FindMySpawnPoints(bool ReturnPointsCloseToAgent)
	{
		List<ReceptacleSpawnPoint> temp = new List<ReceptacleSpawnPoint>();

		foreach(GameObject rtb in ReceptacleTriggerBoxes)
		{
			Contains containsScript = rtb.GetComponent<Contains>();
			temp.AddRange(containsScript.GetValidSpawnPoints(ReturnPointsCloseToAgent));
		}

		if(ReturnPointsCloseToAgent)
		{
			GameObject agent = GameObject.Find("FPSController");

			temp.Sort(delegate(ReceptacleSpawnPoint one, ReceptacleSpawnPoint two)
			{
				return Vector3.Distance(agent.transform.position, one.Point).CompareTo(Vector3.Distance(agent.transform.position, two.Point));
			});
		}

		MySpawnPoints = temp;
	}

	//set ReturnPointsCloseToAgent to true if only points near the agent are wanted
	//set to false if all potential points on the object are wanted
	public List<ReceptacleSpawnPoint> ReturnMySpawnPoints(bool ReturnPointsCloseToAgent)
	{
		FindMySpawnPoints(ReturnPointsCloseToAgent);
		return MySpawnPoints;
	}

	public void ResetContactPointsDictionary() {
		contactPointsDictionary.Clear();
	}

	void OnCollisionEnter (Collision col)	
    {
		contactPointsDictionary[col.collider] = col.contacts;
		
		//this is to enable kinematics if this object hits another object that isKinematic but needs to activate
		//physics uppon being touched/collided

		if(col.transform.GetComponentInParent<SimObjPhysics>())
		{
			//add a check for if this is the handheld object, in which case dont't do this!
			GameObject agent = GameObject.Find("FPSController");
			if(!agent.transform.GetComponent<PhysicsRemoteFPSAgentController>().WhatAmIHolding() == this.transform)
			{
				SimObjPhysics colsop = col.transform.GetComponentInParent<SimObjPhysics>();

				if(colsop.PrimaryProperty == SimObjPrimaryProperty.CanPickup || colsop.PrimaryProperty == SimObjPrimaryProperty.Moveable)
				{
					Rigidbody rb = colsop.transform.GetComponent<Rigidbody>();
					rb.isKinematic = false;
					rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

				}
			}

		}
	}
	void OnCollisionExit (Collision col)	
    {
		contactPointsDictionary.Remove(col.collider);
	}

#if UNITY_EDITOR

	
	[UnityEditor.MenuItem("SimObjectPhysics/Create RB Collider")]
	public static void CreateRBCollider()
	{
		GameObject prefabRoot = Selection.activeGameObject;
		//print(prefabRoot.name);

		GameObject inst = Instantiate(prefabRoot, Selection.activeGameObject.transform, true);

		//inst.transform.SetParent(Selection.activeGameObject.transform);

		inst.name = "rbCol";
		inst.gameObject.AddComponent<Rigidbody>();
		inst.GetComponent<Rigidbody>().isKinematic = true;
		inst.GetComponent<Rigidbody>().useGravity = true;

		//default tag and layer so that nothing is raycast against this. The only thing this exists for is to make physics real
		inst.tag = "Untagged";
		inst.layer = 0;// default layer

		//EditorUtility.GetPrefabParent(Selection.activeGameObject);
		//PrefabUtility.InstantiatePrefab(prefabRoot);
	}

	[UnityEditor.MenuItem("SimObjectPhysics/Change All Lights to Soft")]
	public static void AllOfTheLights()
	{
		Light[] lights = FindObjectsOfType(typeof(Light)) as Light[];
		foreach(Light l in lights)
		{
			l.shadows = LightShadows.Soft;
		}
	}

	[UnityEditor.MenuItem("SimObjectPhysics/Create Sim Obj from Mesh &r")]
	public static void CreateFromMesh()
	{
		GameObject prefabRoot = Selection.activeGameObject;
		GameObject top = new GameObject("changethisname");
		top.transform.position = prefabRoot.transform.position;
		top.transform.rotation = prefabRoot.transform.rotation;

		prefabRoot.transform.SetParent(top.transform);

		SimObjPhysics sop = top.AddComponent<SimObjPhysics>();

		sop.ContextSetUpSimObjPhysics();
	}

	[UnityEditor.MenuItem("SimObjectPhysics/Set Transform Scale to 1 &e")]
	public static void ResetTransformScale()
	{
		GameObject selected = Selection.activeGameObject;

		List <Transform> selectedchildren = new List <Transform>();

		foreach(Transform t in selected.transform)
		{
			//print(t.name);
			selectedchildren.Add(t);
			//t.SetParent(null);
		}

		foreach(Transform yes in selectedchildren)
		{
			yes.SetParent(null);
		}

		selected.transform.localScale = new Vector3 (1, 1, 1);

		foreach(Transform t in selectedchildren)
		{
			t.SetParent(selected.transform);
		}


	}


#endif

	// Use this for initialization
	void Start()
	{
		//XXX For Debug setting up scene, comment out or delete when done settig up scenes
#if UNITY_EDITOR
		List<SimObjSecondaryProperty> temp = new List<SimObjSecondaryProperty>(SecondaryProperties);
		if (temp.Contains(SimObjSecondaryProperty.Receptacle))
		{
			if (ReceptacleTriggerBoxes.Length == 0)
			{
				Debug.LogError(this.name + " is missing ReceptacleTriggerBoxes please hook them up");
			}
		}

		if(temp.Contains(SimObjSecondaryProperty.ObjectSpecificReceptacle))
		{
			if(!gameObject.GetComponent<ObjectSpecificReceptacle>())
			{
				Debug.LogError(this.name + " is missing the ObjectSpecificReceptacle component!");
			}
		}
#endif
		//end debug setup stuff

		OriginalPhysicsMaterialValuesForAllMyColliders = new PhysicsMaterialValues[MyColliders.Length];

		for(int i = 0; i < MyColliders.Length; i++)
		{
			OriginalPhysicsMaterialValuesForAllMyColliders[i] = 
			new PhysicsMaterialValues(MyColliders[i].material.dynamicFriction, MyColliders[i].material.staticFriction, MyColliders[i].material.bounciness);
		}

		Rigidbody rb = gameObject.GetComponent<Rigidbody>();

		RBoriginalAngularDrag = rb.angularDrag;
		RBoriginalDrag = rb.drag;
	}

	public bool DoesThisObjectHaveThisSecondaryProperty(SimObjSecondaryProperty prop)
	{
		bool result = false;
		List<SimObjSecondaryProperty> temp = new List<SimObjSecondaryProperty>(SecondaryProperties);

		if (temp.Contains(prop))
		{
			result = true;
		}

		return result;
	}
	// Update is called once per frame
	void Update()
	{

		//this is overriden by the Agent when doing the Visibility Sphere test
		//XXX Probably don't need to do this EVERY update loop except in editor for debug purposes
		isVisible = false;
		isInteractable = false;

		//if this object has come to rest, reset it's collision detection mode to discrete
		Rigidbody rb = gameObject.GetComponent<Rigidbody>();
		if((rb.IsSleeping() == true) && (rb.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic))
		{
			//print("settling");
			rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
		}
	}

	private void FixedUpdate()
	{
		isColliding = false;
	}

	//used for throwing the sim object, or anything that requires adding force for some reason
	public void ApplyForce(ServerAction action)
	{
		Vector3 dir = new Vector3(action.x, action.y, action.z);
		Rigidbody myrb = gameObject.GetComponent<Rigidbody>();
		myrb.isKinematic = false;
		myrb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
		myrb.AddForce(dir * action.moveMagnitude);
	}

	//returns a game object list of all sim objects contained by this object if it is a receptacle
	public List<GameObject> Contains_GameObject()
	{
		List<SimObjSecondaryProperty> sspList = new List<SimObjSecondaryProperty>(SecondaryProperties);

		List<GameObject> objs = new List<GameObject>();

		//is this object a receptacle?
		if (sspList.Contains(SimObjSecondaryProperty.Receptacle))
		{
			//this is a receptacle, now populate objs list of contained objets to return below
			if (ReceptacleTriggerBoxes != null)
			{
				//do this once per ReceptacleTriggerBox referenced by this object
				foreach (GameObject rtb in ReceptacleTriggerBoxes)
				{
					//now go through every object each ReceptacleTriggerBox is keeping track of and add their string UniqueID to objs
					foreach (SimObjPhysics sop in rtb.GetComponent<Contains>().CurrentlyContainedObjects())
					{
						//don't add repeats
						if (!objs.Contains(sop.gameObject))
							objs.Add(sop.gameObject);
					}
				}
			}
		}

		return objs;
	}

	//if this is a receptacle object, return list of references to all objects currently contained
	public List<SimObjPhysics> ContainsGameObject()
	{
		List<SimObjPhysics> objs = new List<SimObjPhysics>();

		if(DoesThisObjectHaveThisSecondaryProperty(SimObjSecondaryProperty.Receptacle))
		{
			if(ReceptacleTriggerBoxes != null)
			{
				foreach (GameObject go in ReceptacleTriggerBoxes)
				{
					foreach(SimObjPhysics sop in go.GetComponent<Contains>().CurrentlyContainedObjects())
					{
						if(!objs.Contains(sop))
						{
							objs.Add(sop);
						}
					}
				}
			}
		}

		return objs;
	}

	//if this is a receptacle object, check what is inside the Receptacle
	//make sure to return array of strings so that this info can be put into MetaData
	public List<string> Contains()
	{
		//grab a list of all secondary properties of this object
		List<SimObjSecondaryProperty> sspList = new List<SimObjSecondaryProperty>(SecondaryProperties);

		List<string> objs = new List<string>();

		//is this object a receptacle?
		if (sspList.Contains(SimObjSecondaryProperty.Receptacle))
		{
			//this is a receptacle, now populate objs list of contained objets to return below
			if (ReceptacleTriggerBoxes != null)
			{
				//do this once per ReceptacleTriggerBox referenced by this object
				foreach (GameObject rtb in ReceptacleTriggerBoxes)
				{
					//now go through every object each ReceptacleTriggerBox is keeping track of and add their string UniqueID to objs
					foreach (string id in rtb.GetComponent<Contains>().CurrentlyContainedUniqueIDs())
					{
						//don't add repeats
						if (!objs.Contains(id))
							objs.Add(id);
					}
					//objs.Add(rtb.GetComponent<Contains>().CurrentlyContainedUniqueIDs()); 
				}

				/////UNCOMMENT BELOW if you want a list of what receptacles contian what objects on start
				// #if UNITY_EDITOR

				// if (objs.Count != 0)
				// {
				// 	//print the objs for now just to check in editor
				// 	string result = UniqueID + " contains: ";

				// 	foreach (string s in objs)
				// 	{
				// 		result += s + ", ";
				// 	}

				// 	Debug.Log(result);
				// }

				// else
				// {
				// 	Debug.Log(UniqueID + " is empty");
				// }
				// #endif
				
				return objs;
			}

			else
			{
				Debug.Log("No Receptacle Trigger Box!");
				return objs;
			}
		}

		else
		{
			Debug.Log(gameObject.name + " is not a Receptacle!");
			return objs;
		}
	}

	public void OnTriggerStay(Collider other)
	{

		if(other.gameObject.tag == "HighFriction" && (PrimaryProperty == SimObjPrimaryProperty.CanPickup || PrimaryProperty == SimObjPrimaryProperty.Moveable))
		{
			Rigidbody rb = gameObject.GetComponent<Rigidbody>();

			//add something so that drag/angular drag isn't reset if we haven't set it on the object yet
			rb.drag = HFrbdrag;
			rb.angularDrag = HFrbangulardrag;
			
			foreach (Collider col in MyColliders)
			{
				col.material.dynamicFriction = HFdynamicfriction;
				col.material.staticFriction = HFstaticfriction;
				col.material.bounciness = HFbounciness;
			}
		}

		//ignore collision of ghosted receptacle trigger boxes
		//because of this MAKE SURE ALL receptacle trigger boxes are tagged as "Receptacle," they should be by default
		//do this flag first so that the check against non Player objects overrides it in the right order
		if (other.tag == "Receptacle")
		{
			isColliding = false;
			return;
		}

		//make sure nothing is dropped while inside the agent (the agent will try to "push(?)" it out and it will fall in unpredictable ways
		else if (other.tag == "Player" && other.name == "FPSController")
		{
			isColliding = true;
			return;
		}

		//this is hitting something else so it must be colliding at this point!
		else if (other.tag != "Player")
		{
			isColliding = true;
			return;
		}
	}

	public void OnTriggerExit(Collider other)
	{
		if(other.gameObject.tag == "HighFriction" && (PrimaryProperty == SimObjPrimaryProperty.CanPickup || PrimaryProperty == SimObjPrimaryProperty.Moveable))
		{
			//print( "resetting to default trigger exit");

			Rigidbody rb = gameObject.GetComponent<Rigidbody>();

			rb.drag = RBoriginalDrag;
			rb.angularDrag = RBoriginalAngularDrag;
			
			for(int i = 0; i < MyColliders.Length; i++)
			{
				MyColliders[i].material.dynamicFriction = OriginalPhysicsMaterialValuesForAllMyColliders[i].DynamicFriction;
				MyColliders[i].material.staticFriction = OriginalPhysicsMaterialValuesForAllMyColliders[i].StaticFriction;
				MyColliders[i].material.bounciness = OriginalPhysicsMaterialValuesForAllMyColliders[i].Bounciness;
			}
		}
	}

#if UNITY_EDITOR
	void OnDrawGizmos()
	{
		Gizmos.color = Color.white;

		//if this object is in visibile range and not blocked by any other object, it is visible
		//visible drawn in yellow
		if (isVisible == true)
		{
			MeshFilter mf = gameObject.GetComponentInChildren<MeshFilter>(false);
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireMesh(mf.sharedMesh, -1, mf.transform.position, mf.transform.rotation, mf.transform.lossyScale);
		}

		//interactable drawn in magenta
		if (isInteractable == true)
		{
			MeshFilter mf = gameObject.GetComponentInChildren<MeshFilter>(false);
			Gizmos.color = Color.magenta;
			Gizmos.DrawWireMesh(mf.sharedMesh, -1, mf.transform.position, mf.transform.rotation, mf.transform.lossyScale);
		}

		//draw visibility points for editor
		Gizmos.color = Color.yellow;
		if (VisibilityPoints.Length > 0)
		{
			foreach (Transform t in VisibilityPoints)
			{
				Gizmos.DrawSphere(t.position, 0.01f);

			}
		}
	}

	//CONTEXT MENU STUFF FOR SETTING UP SIM OBJECTS
	//RIGHT CLICK this script in the inspector to reveal these options
	[ContextMenu("Cabinet")]
	void SetUpCabinet()
	{
		Type = SimObjType.Cabinet;
		PrimaryProperty = SimObjPrimaryProperty.Static;

		SecondaryProperties = new SimObjSecondaryProperty[2];
		SecondaryProperties[0] = SimObjSecondaryProperty.CanOpen;
		SecondaryProperties[1] = SimObjSecondaryProperty.Receptacle;

		gameObject.transform.tag = "SimObjPhysics";
		gameObject.layer = 8;

		if (!gameObject.GetComponent<Rigidbody>())
			gameObject.AddComponent<Rigidbody>();

		this.GetComponent<Rigidbody>().isKinematic = true;

		if (!gameObject.GetComponent<CanOpen_Object>())
		{
			gameObject.AddComponent<CanOpen_Object>();
			gameObject.GetComponent<CanOpen_Object>().SetMovementToRotate();
		}

		//if (!gameObject.GetComponent<MovingPart>())
		//gameObject.AddComponent<MovingPart>();

		List<GameObject> cols = new List<GameObject>();
		//List<GameObject> tcols = new List<GameObject>();
		List<Transform> vpoints = new List<Transform>();
		List<GameObject> recepboxes = new List<GameObject>();

		List<GameObject> movparts = new List<GameObject>();

		List<Vector3> openPositions = new List<Vector3>();

		Transform door = transform.Find("CabinetDoor");


		if(!gameObject.transform.Find("StaticVisPoints"))
		{
			GameObject svp = new GameObject("StaticVisPoints");
			svp.transform.position = gameObject.transform.position;
			svp.transform.SetParent(gameObject.transform);

			GameObject vp = new GameObject("vPoint");
			vp.transform.position = svp.transform.position;
			vp.transform.SetParent(svp.transform);
		}

		if(!door.Find("Colliders"))
		{
			GameObject col = new GameObject("Colliders");
			col.transform.position = door.position;
			col.transform.SetParent(door);

			GameObject coll = new GameObject("Col");
			coll.AddComponent<BoxCollider>();
			coll.transform.tag = "SimObjPhysics";
			coll.layer = 8;
			coll.transform.position = col.transform.position;
			coll.transform.SetParent(col.transform);
		}

		if(!door.Find("VisibilityPoints"))
		{
			GameObject VisPoints = new GameObject("VisibilityPoints");
			VisPoints.transform.position = door.position;
			VisPoints.transform.SetParent(door);

			GameObject vp = new GameObject("VisPoint");
			vp.transform.position = VisPoints.transform.position;
			vp.transform.SetParent(VisPoints.transform);
		}

		else
		{
			Transform VisPoints = door.Find("VisibilityPoints");
			foreach (Transform child in VisPoints)
			{
				vpoints.Add(child);
				child.gameObject.tag = "Untagged";
				child.gameObject.layer = 8;
			}
		}
		////////////////////////

		foreach (Transform child in gameObject.transform)
		{
			if (child.name == "StaticVisPoints")
			{
				foreach (Transform svp in child)
				{
					if (!vpoints.Contains(svp))
						vpoints.Add(svp);
				}
			}

			if (child.name == "ReceptacleTriggerBox")
			{
				//print("check");
				if (!recepboxes.Contains(child.gameObject))
					recepboxes.Add(child.gameObject);
			}

			//found the cabinet door, go into it and populate triggerboxes, colliders, t colliders, and vis points
			if (child.name == "CabinetDoor")
			{
				if (child.GetComponent<Rigidbody>())
					DestroyImmediate(child.GetComponent<Rigidbody>(), true);

				if (child.GetComponent<SimObjPhysics>())
					DestroyImmediate(child.GetComponent<SimObjPhysics>(), true);

				if (!movparts.Contains(child.gameObject))
				{
					movparts.Add(child.gameObject);
				}

				foreach (Transform c in child)
				{
					if (c.name == "Colliders")
					{
						foreach (Transform col in c)
						{
							if (!cols.Contains(col.gameObject))
								cols.Add(col.gameObject);

							if (col.childCount == 0)
							{
								GameObject prefabRoot = col.gameObject;

								GameObject inst = Instantiate(prefabRoot, col.gameObject.transform, true);

								//inst.transform.SetParent(Selection.activeGameObject.transform);

								inst.name = "rbCol";
								inst.gameObject.AddComponent<Rigidbody>();
								inst.GetComponent<Rigidbody>().isKinematic = true;
								inst.GetComponent<Rigidbody>().useGravity = true;

								//default tag and layer so that nothing is raycast against this. The only thing this exists for is to make physics real
								inst.tag = "Untagged";
								inst.layer = 0;// default layer
							}
						}
					}

					// if (c.name == "TriggerColliders")
					// {
					// 	foreach (Transform col in c)
					// 	{
					// 		if (!tcols.Contains(col.gameObject))
					// 			tcols.Add(col.gameObject);
					// 	}
					// }

					if (c.name == "VisibilityPoints")
					{
						foreach (Transform col in c)
						{
							if (!vpoints.Contains(col.transform))
								vpoints.Add(col.transform);
						}
					}
				}
			}
		}

		VisibilityPoints = vpoints.ToArray();
		//MyColliders = cols.ToArray();
		//MyTriggerColliders = tcols.ToArray();
		ReceptacleTriggerBoxes = recepboxes.ToArray();


		gameObject.GetComponent<CanOpen_Object>().MovingParts = movparts.ToArray();
		gameObject.GetComponent<CanOpen_Object>().openPositions = new Vector3[movparts.Count];
		gameObject.GetComponent<CanOpen_Object>().closedPositions = new Vector3[movparts.Count];

		if (openPositions.Count != 0)
			gameObject.GetComponent<CanOpen_Object>().openPositions = openPositions.ToArray();


		//this.GetComponent<CanOpen>().SetMovementToRotate();
	}
	[ContextMenu("Table")]
	void SetUpTable()
	{
		this.Type = SimObjType.TableTop;
		this.PrimaryProperty = SimObjPrimaryProperty.Static;
		this.SecondaryProperties = new SimObjSecondaryProperty[] {SimObjSecondaryProperty.Receptacle};
		
		ContextSetUpSimObjPhysics();

		// GameObject inst = Instantiate(new GameObject(), gameObject.transform, true);
		// inst.AddComponent<BoxCollider>();
		if(!gameObject.transform.Find("BoundingBox"))
		{
			GameObject bb = new GameObject("BoundingBox");
			bb.transform.position = gameObject.transform.position;
			bb.transform.SetParent(gameObject.transform);
			bb.AddComponent<BoxCollider>();
			bb.GetComponent<BoxCollider>().enabled = false;
			bb.tag = "Untagged";
			bb.layer = 0;

			BoundingBox = bb;
		}

		List<GameObject> recepboxes = new List<GameObject>();

		foreach(Transform t in gameObject.transform)
		{
			if(t.GetComponent<Contains>())
			{
				recepboxes.Add(t.gameObject);
			}
		}

		ReceptacleTriggerBoxes = recepboxes.ToArray();
	}

	[ContextMenu("Laptop")]
	void LaptopSetupContext()
	{
		this.Type = SimObjType.Laptop;
		this.PrimaryProperty = SimObjPrimaryProperty.CanPickup;

		this.SecondaryProperties = new SimObjSecondaryProperty[] 
		{SimObjSecondaryProperty.CanOpen, SimObjSecondaryProperty.CanToggleOnOff};

		if (!gameObject.GetComponent<Rigidbody>())
			gameObject.AddComponent<Rigidbody>();

		this.GetComponent<Rigidbody>().isKinematic = true;
		
		List<Transform> vpoints = new List<Transform>();

		ContextSetUpSimObjPhysics();

		if(!gameObject.GetComponent<CanOpen_Object>())
		{
			//CanOpen_Object coo = 
			gameObject.AddComponent<CanOpen_Object>();
			
		}

		if(!gameObject.GetComponent<CanToggleOnOff>())
		{
			//CanToggleOnOff ctoo = 
			gameObject.AddComponent<CanToggleOnOff>();
		}
		// if(!gameObject.transform.Find("AttachPoint"))
		// {
		// 	GameObject ap = new GameObject("AttachPoint");
		// 	ap.transform.position = gameObject.transform.position;
		// 	ap.transform.SetParent(gameObject.transform);
			
		// }	
				
		// ObjectSpecificReceptacle osr;
		// if(!gameObject.GetComponent<ObjectSpecificReceptacle>())
		// {
		// 	osr = gameObject.AddComponent<ObjectSpecificReceptacle>();
		// 	osr.SpecificTypes = new SimObjType[] {SimObjType.Mug};
		// }

		// else
		// {
		// 	osr = gameObject.GetComponent<ObjectSpecificReceptacle>();
		// }

		foreach(Transform child in gameObject.transform)
		{

			if (child.name == "VisibilityPoints")
			{
				foreach (Transform col in child)
				{
					if (!vpoints.Contains(col.transform))
						vpoints.Add(col.transform);
				}
			}

			// if(child.name == "AttachPoint")
			// {
			// 	osr.attachPoint = child.transform;
			// }
		}

		VisibilityPoints = vpoints.ToArray();
	}

	[ContextMenu("Drawer")]
	void SetUpDrawer()
	{
		this.Type = SimObjType.Drawer;
		this.PrimaryProperty = SimObjPrimaryProperty.Static;
		this.SecondaryProperties = new SimObjSecondaryProperty[] {SimObjSecondaryProperty.Receptacle, SimObjSecondaryProperty.CanOpen};

		ContextSetUpSimObjPhysics();

		//delete the trigger colliders and bounding box here
		//also probably edit ContextSetupSimObjPhysics to not create trigger colliders anymore

		if (!gameObject.GetComponent<CanOpen_Object>())
		{
			gameObject.AddComponent<CanOpen_Object>();
		}
		CanOpen_Object coo = gameObject.GetComponent<CanOpen_Object>();

		GameObject[] myobject = new GameObject[] { gameObject };
		coo.MovingParts = myobject;
		coo.closedPositions = new Vector3[coo.MovingParts.Length];
		coo.openPositions = new Vector3[coo.MovingParts.Length];

		coo.closedPositions[0] = gameObject.transform.localPosition;
		coo.openPositions[0] = gameObject.transform.localPosition;

		List<GameObject> recepboxes = new List<GameObject>();

		foreach(Transform t in gameObject.transform)
		{
			if(t.name == "BoundingBox")
			{
				DestroyImmediate(t.gameObject, true);
				break;
			}
			
			if(t.name == "ReceptacleTriggerBox")
			{
				if(!recepboxes.Contains(t.gameObject))
				{
					recepboxes.Add(t.gameObject);
				}
			}

			// if(t.name == "Colliders")
			// {
			// 	if(!gameObject.transform.Find("TriggerColliders"))
			// 	{
			// 		GameObject inst = Instantiate(t.gameObject, gameObject.transform, true);
			// 		inst.name = "TriggerColliders";
			// 		foreach(Transform yes in inst.transform)
			// 		{
			// 			yes.GetComponent<BoxCollider>().isTrigger = true;
			// 		}
			// 	}

			// 	else
			// 	{
			// 		DestroyImmediate(gameObject.transform.Find("TriggerColliders").gameObject);
			// 		GameObject inst = Instantiate(t.gameObject, gameObject.transform, true);
			// 		inst.name = "TriggerColliders";
			// 		foreach(Transform yes in inst.transform)
			// 		{
			// 			yes.GetComponent<BoxCollider>().isTrigger = true;
			// 		}
			// 	}

			// }
		}

		ReceptacleTriggerBoxes = recepboxes.ToArray();

		//make trigger colliders, if there is "Colliders" already, duplicate them, parant to this object
		//then go through and set them all to istrigger.

	}

	//[ContextMenu("Find BoundingBox")]
	void ContextFindBoundingBox()
	{
		BoundingBox = gameObject.transform.Find("BoundingBox").gameObject;

	}

	//[ContextMenu("Set Up Microwave")]
	void ContextSetUpMicrowave()
	{
		this.Type = SimObjType.Microwave;
		this.PrimaryProperty = SimObjPrimaryProperty.Static;

		this.SecondaryProperties = new SimObjSecondaryProperty[2];
		this.SecondaryProperties[0] = SimObjSecondaryProperty.Receptacle;
		this.SecondaryProperties[1] = SimObjSecondaryProperty.CanOpen;

		if(!gameObject.transform.Find("BoundingBox"))
		{
			GameObject bb = new GameObject("BoundingBox");
			bb.transform.position = gameObject.transform.position;
			bb.transform.SetParent(gameObject.transform);
			bb.AddComponent<BoxCollider>();
			bb.GetComponent<BoxCollider>().enabled = false;
			bb.tag = "Untagged";
			bb.layer = 0;

			BoundingBox = bb;
		}

		else
		{
			BoundingBox = gameObject.transform.Find("BoundingBox").gameObject;
		}

		if(!gameObject.GetComponent<CanOpen_Object>())
		{
			gameObject.AddComponent<CanOpen_Object>();
		}

		List<Transform> vplist = new List<Transform>();

		if(!gameObject.transform.Find("StaticVisPoints"))
		{
			GameObject svp = new GameObject("StaticVisPoints");
			svp.transform.position = gameObject.transform.position;
			svp.transform.SetParent(gameObject.transform);

			GameObject vp = new GameObject("vPoint");
			vp.transform.position = svp.transform.position;
			vp.transform.SetParent(svp.transform);
		}

		else
		{
			Transform vp = gameObject.transform.Find("StaticVisPoints");
			foreach (Transform child in vp)
			{
				vplist.Add(child);

				//set correct tag and layer for each object
				child.gameObject.tag = "Untagged";
				child.gameObject.layer = 8;
			}
		}

		Transform door = gameObject.transform.Find("Door");
		if(!door.Find("Col"))
		{
			GameObject col = new GameObject("Col");
			col.transform.position = door.transform.position;
			col.transform.SetParent(door.transform);

			col.AddComponent<BoxCollider>();

			col.transform.tag = "SimObjPhysics";
			col.layer = 8;

		}

		if(!door.Find("VisPoints"))
		{
						//empty to hold all visibility points
			GameObject vp = new GameObject("VisPoints");
			vp.transform.position = door.transform.position;
			vp.transform.SetParent(door.transform);

			//create first Visibility Point to work with
			GameObject vpc = new GameObject("vPoint");
			vpc.transform.position = vp.transform.position;
			vpc.transform.SetParent(vp.transform);
		}

		else
		{
			Transform vp = door.Find("VisPoints");
			foreach (Transform child in vp)
			{
				vplist.Add(child);

				//set correct tag and layer for each object
				child.gameObject.tag = "Untagged";
				child.gameObject.layer = 8;
			}
		}

		VisibilityPoints = vplist.ToArray();

		CanOpen_Object coo = gameObject.GetComponent<CanOpen_Object>();
		coo.MovingParts = new GameObject[] {door.transform.gameObject};
		coo.openPositions = new Vector3[] {new Vector3(0, 90, 0)};
		coo.closedPositions = new Vector3[] {Vector3.zero};
		coo.SetMovementToRotate();

		if(gameObject.transform.Find("ReceptacleTriggerBox"))
		{
			GameObject[] rtb = new GameObject[] {gameObject.transform.Find("ReceptacleTriggerBox").transform.gameObject};
			ReceptacleTriggerBoxes = rtb;
		}
	}

	[ContextMenu("Static Mesh Collider with Receptacle")]
	void SetUpSimObjWithStaticMeshCollider()
	{
		if (this.Type == SimObjType.Undefined || this.PrimaryProperty == SimObjPrimaryProperty.Undefined)
		{
			Debug.Log("Type / Primary Property is missing");
			return;
		}
		//set up this object ot have the right tag and layer
		gameObject.tag = "SimObjPhysics";
		gameObject.layer = 8;

		if (!gameObject.GetComponent<Rigidbody>())
			gameObject.AddComponent<Rigidbody>();

		gameObject.GetComponent<Rigidbody>().isKinematic = true;
		if (!gameObject.transform.Find("VisibilityPoints"))
		{
			//empty to hold all visibility points
			GameObject vp = new GameObject("VisibilityPoints");
			vp.transform.position = gameObject.transform.position;
			vp.transform.SetParent(gameObject.transform);

			//create first Visibility Point to work with
			GameObject vpc = new GameObject("vPoint");
			vpc.transform.position = vp.transform.position;
			vpc.transform.SetParent(vp.transform);
		}

		if (!gameObject.transform.Find("BoundingBox"))
		{
			GameObject rac = new GameObject("BoundingBox");
			rac.transform.position = gameObject.transform.position;
			rac.transform.SetParent(gameObject.transform);
			rac.AddComponent<BoxCollider>();
			rac.GetComponent<BoxCollider>().enabled = false;
		}

		List <GameObject> rtbList = new List <GameObject>();

		foreach (Transform t in gameObject.transform)
		{
			if(t.GetComponent<MeshCollider>())
			{
				t.gameObject.tag = "SimObjPhysics";
				t.gameObject.layer = 8;

				//now check if it has pillows or something?
				foreach(Transform yes in t)
				{
					if(yes.GetComponent<MeshCollider>())
					{
						yes.gameObject.tag = "SimObjPhysics";
						yes.gameObject.layer = 8;
					}
				}
			}

			if(t.GetComponent<Contains>())
			{
				rtbList.Add(t.gameObject);
			}
		}

		ReceptacleTriggerBoxes = rtbList.ToArray();

		ContextSetUpVisibilityPoints();
		ContextSetUpBoundingBox();
	}

	//[UnityEditor.MenuItem("SimObjectPhysics/AppleSlice")]
	public static void ContextSetupAppleSlice()
	{
		GameObject prefabRoot = Selection.activeGameObject;
		GameObject c = new GameObject("AppleSlice");
		c.transform.position = prefabRoot.transform.position;
		//prefabRoot.transform.position = c.transform.position;
		prefabRoot.transform.SetParent(c.transform);
		prefabRoot.name = "Mesh";

		if(!c.GetComponent<SimObjPhysics>())
		{
			c.AddComponent<SimObjPhysics>();
		}

		if(c.GetComponent<SimObjPhysics>())
		{
			SimObjPhysics sop = c.GetComponent<SimObjPhysics>();
			sop.PrimaryProperty = SimObjPrimaryProperty.CanPickup;
			sop.Type = SimObjType.AppleSliced;
			//sop.SecondaryProperties = new SimObjSecondaryProperty[] {SimObjSecondaryProperty};
		}
		c.tag = "SimObjPhysics";
		c.layer = 8;

		if (!c.GetComponent<Rigidbody>())
			c.AddComponent<Rigidbody>();

		if (!c.transform.Find("Colliders"))
		{
			GameObject col = new GameObject("Colliders");
			col.transform.position = c.transform.position;
			col.transform.SetParent(c.transform);
            
			GameObject cc = new GameObject("Col");
			cc.transform.position = col.transform.position;
			cc.transform.SetParent(col.transform);
			cc.AddComponent<BoxCollider>();
			cc.tag = "SimObjPhysics";
			cc.layer = 8;
		}

		if (!c.transform.Find("VisibilityPoints"))
		{
			//empty to hold all visibility points
			GameObject vp = new GameObject("VisibilityPoints");
			vp.transform.position = c.transform.position;
			vp.transform.SetParent(c.transform);

			//create first Visibility Point to work with
			GameObject vpc = new GameObject("vPoint");
			vpc.transform.position = vp.transform.position;
			vpc.transform.SetParent(vp.transform);
		}

		if (!c.transform.Find("BoundingBox"))
		{
			GameObject rac = new GameObject("BoundingBox");
			rac.transform.position = c.transform.position;
			rac.transform.SetParent(c.transform);
			rac.AddComponent<BoxCollider>();
			rac.GetComponent<BoxCollider>().enabled = false;
		}
		
		//c.GetComponent<SimObjPhysics>().AppleSetupReferences();
	}

	//[UnityEditor.MenuItem("SimObjectPhysics/LightSwitch")]
	public static void ContextSetupLightSwitch()
	{
		GameObject prefabRoot = Selection.activeGameObject;
		GameObject c = new GameObject("LightSwitch");
		c.transform.position = prefabRoot.transform.position;
		prefabRoot.transform.SetParent(c.transform);
		prefabRoot.name = "Mesh";

		if(!c.GetComponent<SimObjPhysics>())
		{
			c.AddComponent<SimObjPhysics>();
		}

		if(c.GetComponent<SimObjPhysics>())
		{
			SimObjPhysics sop = c.GetComponent<SimObjPhysics>();
			sop.PrimaryProperty = SimObjPrimaryProperty.Static;
			sop.Type = SimObjType.LightSwitch;
			sop.SecondaryProperties = new SimObjSecondaryProperty[] {SimObjSecondaryProperty.CanToggleOnOff};
		}

		c.tag = "SimObjPhysics";
		c.layer = 8;
		c.isStatic = true;

		if (!c.GetComponent<Rigidbody>())
		{
			Rigidbody rb = c.AddComponent<Rigidbody>();
			rb.isKinematic = true;
		}

		if (!c.transform.Find("Colliders"))
		{
			GameObject col = new GameObject("Colliders");
			col.transform.position = c.transform.position;
			col.transform.SetParent(c.transform);
            
			GameObject cc = new GameObject("Col");
			cc.transform.position = col.transform.position;
			cc.transform.SetParent(col.transform);
			cc.AddComponent<BoxCollider>();
			cc.tag = "SimObjPhysics";
			cc.layer = 8;
		}

		if (!c.transform.Find("VisibilityPoints"))
		{
			//empty to hold all visibility points
			GameObject vp = new GameObject("VisibilityPoints");
			vp.transform.position = c.transform.position;
			vp.transform.SetParent(c.transform);

			//create first Visibility Point to work with
			GameObject vpc = new GameObject("vPoint");
			vpc.transform.position = vp.transform.position;
			vpc.transform.SetParent(vp.transform);
		}

		c.GetComponent<SimObjPhysics>().SetupCollidersVisPoints();
		
		//add the CanToggleOnOff component and set it up with correct values
		if(!c.GetComponent<CanToggleOnOff>())
		{	
			CanToggleOnOff ctoo = c.AddComponent<CanToggleOnOff>();

			List<GameObject> childmeshes = new List<GameObject>();
			List<Vector3> childRotations = new List<Vector3>();

			foreach(Transform t in c.transform)
			{
				if(t.name == "Mesh")
				{
					foreach(Transform tt in t)
					{
						childmeshes.Add(tt.gameObject);
						childRotations.Add(tt.transform.localEulerAngles);
					}
				}
			}

			ctoo.MovingParts = childmeshes.ToArray();
			ctoo.OnPositions = childRotations.ToArray();
			ctoo.OffPositions = new Vector3[ctoo.MovingParts.Length];
			ctoo.SetMovementToRotate();
		}
	}


	[ContextMenu("Setup Colliders And VisPoints")]
	void SetupCollidersVisPoints()
	{
		ContextSetUpColliders();
		ContextSetUpVisibilityPoints();
	}

	//[UnityEditor.MenuItem("SimObjectPhysics/Toaster")]
	public static void ContextSetupToaster()
	{
		GameObject prefabRoot = Selection.activeGameObject;
		GameObject c = new GameObject("Toaster_");
		c.transform.position = prefabRoot.transform.position;
		//prefabRoot.transform.position = c.transform.position;
		prefabRoot.transform.SetParent(c.transform);
		prefabRoot.name = "Mesh";

		if(!c.GetComponent<SimObjPhysics>())
		{
			c.AddComponent<SimObjPhysics>();
		}

		if(c.GetComponent<SimObjPhysics>())
		{
			SimObjPhysics sop = c.GetComponent<SimObjPhysics>();
			sop.PrimaryProperty = SimObjPrimaryProperty.Static;
			sop.Type = SimObjType.Toaster;
			//sop.SecondaryProperties = new SimObjSecondaryProperty[] {SimObjSecondaryProperty.CanBeSliced};
		}

		c.tag = "SimObjPhysics";
		c.layer = 8;

		if (!c.GetComponent<Rigidbody>())
			c.AddComponent<Rigidbody>();

		if (!c.transform.Find("Colliders"))
		{
			GameObject col = new GameObject("Colliders");
			col.transform.position = c.transform.position;
			col.transform.SetParent(c.transform);
            
			GameObject cc = new GameObject("Col");
			cc.transform.position = col.transform.position;
			cc.transform.SetParent(col.transform);
			cc.AddComponent<BoxCollider>();
			cc.tag = "SimObjPhysics";
			cc.layer = 8;
		}

		if (!c.transform.Find("VisibilityPoints"))
		{
			//empty to hold all visibility points
			GameObject vp = new GameObject("VisibilityPoints");
			vp.transform.position = c.transform.position;
			vp.transform.SetParent(c.transform);

			//create first Visibility Point to work with
			GameObject vpc = new GameObject("vPoint");
			vpc.transform.position = vp.transform.position;
			vpc.transform.SetParent(vp.transform);
		}

		if (!c.transform.Find("BoundingBox"))
		{
			GameObject rac = new GameObject("BoundingBox");
			rac.transform.position = c.transform.position;
			rac.transform.SetParent(c.transform);
			rac.AddComponent<BoxCollider>();
			rac.GetComponent<BoxCollider>().enabled = false;
		}

		c.GetComponent<SimObjPhysics>().ToasterSetupReferences();
	}

	//[ContextMenu("Toaster Setup References")]
	void ToasterSetupReferences()
	{
		ContextSetUpColliders();
		ContextSetUpVisibilityPoints();
		ContextSetUpBoundingBox();

		foreach(Transform t in gameObject.transform)
		{
			if(t.name == "Colliders")
			{
				if(!gameObject.transform.Find("TriggerColliders"))
				{
					GameObject inst = Instantiate(t.gameObject, gameObject.transform, true);
					inst.name = "TriggerColliders";
					foreach(Transform yes in inst.transform)
					{
						yes.GetComponent<Collider>().isTrigger = true;
					}
				}
				else
				{
					DestroyImmediate(gameObject.transform.Find("TriggerColliders").gameObject);
					GameObject inst = Instantiate(t.gameObject, gameObject.transform, true);
					inst.name = "TriggerColliders";
					foreach(Transform yes in inst.transform)
					{
						yes.GetComponent<Collider>().isTrigger = true;
					}
				}
			}
		}
	}

	[ContextMenu("Setup")]
	void ContextSetUpSimObjPhysics()
	{
		if (this.Type == SimObjType.Undefined || this.PrimaryProperty == SimObjPrimaryProperty.Undefined)
		{
			Debug.Log("Type / Primary Property is missing");
			return;
		}
		//set up this object ot have the right tag and layer
		gameObject.tag = "SimObjPhysics";
		gameObject.layer = 8;

		if (!gameObject.GetComponent<Rigidbody>())
			gameObject.AddComponent<Rigidbody>();

		gameObject.GetComponent<Rigidbody>().isKinematic = true;

		if (!gameObject.transform.Find("Colliders"))
		{
			GameObject c = new GameObject("Colliders");
			c.transform.position = gameObject.transform.position;
			c.transform.SetParent(gameObject.transform);
            
			GameObject cc = new GameObject("Col");
			cc.transform.position = c.transform.position;
			cc.transform.SetParent(c.transform);
			//cc.AddComponent<CapsuleCollider>();
			cc.AddComponent<BoxCollider>();
			cc.tag = "SimObjPhysics";
			cc.layer = 8;
		}

		if (!gameObject.transform.Find("VisibilityPoints"))
		{
			//empty to hold all visibility points
			GameObject vp = new GameObject("VisibilityPoints");
			vp.transform.position = gameObject.transform.position;
			vp.transform.SetParent(gameObject.transform);

			//create first Visibility Point to work with
			GameObject vpc = new GameObject("vPoint");
			vpc.transform.position = vp.transform.position;
			vpc.transform.SetParent(vp.transform);
		}

		if (!gameObject.transform.Find("BoundingBox"))
		{
			GameObject rac = new GameObject("BoundingBox");
			rac.transform.position = gameObject.transform.position;
			rac.transform.SetParent(gameObject.transform);
			rac.AddComponent<BoxCollider>();
			rac.GetComponent<BoxCollider>().enabled = false;
		}

		List<GameObject> recepboxes = new List<GameObject>();

		foreach(Transform t in gameObject.transform)
		{
			if(t.GetComponent<Contains>())
			{
				if(!recepboxes.Contains(t.gameObject))
				{
					recepboxes.Add(t.gameObject);
				}
			}

			if(t.name == "Colliders")
			{
				if(!gameObject.transform.Find("TriggerColliders"))
				{
					GameObject inst = Instantiate(t.gameObject, gameObject.transform, true);
					inst.name = "TriggerColliders";
					foreach(Transform yes in inst.transform)
					{
						yes.GetComponent<Collider>().isTrigger = true;
					}
				}
				else
				{
					DestroyImmediate(gameObject.transform.Find("TriggerColliders").gameObject);
					GameObject inst = Instantiate(t.gameObject, gameObject.transform, true);
					inst.name = "TriggerColliders";
					foreach(Transform yes in inst.transform)
					{
						yes.GetComponent<Collider>().isTrigger = true;
					}
				}
			}
		}

		ReceptacleTriggerBoxes = recepboxes.ToArray();

		ContextSetUpColliders();
		//ContextSetUpTriggerColliders();
		ContextSetUpVisibilityPoints();
		//ContextSetUpInteractionPoints();
		ContextSetUpBoundingBox();
	}

	

	//[ContextMenu("Set Up Colliders")]
	public void ContextSetUpColliders()
	{
		if (transform.Find("Colliders"))
		{
			Transform Colliders = transform.Find("Colliders");

			List<Collider> listColliders = new List<Collider>();

			foreach (Transform child in Colliders)
			{
				//list.toarray
				listColliders.Add(child.GetComponent<Collider>());

				//set correct tag and layer for each object
				//also ensure all colliders are NOT trigger
				child.gameObject.tag = "SimObjPhysics";
				child.gameObject.layer = 8;

				if (child.GetComponent<Collider>())
				{
					child.GetComponent<Collider>().enabled = true;
					child.GetComponent<Collider>().isTrigger = false;
				}

			}

			MyColliders = listColliders.ToArray();
		}
	}

	//[ContextMenu("Set Up TriggerColliders")]
	void ContextSetUpTriggerColliders()
	{
		if (transform.Find("TriggerColliders"))
		{
			Transform tc = transform.Find("TriggerColliders");

			List<GameObject> listtc = new List<GameObject>();

			foreach (Transform child in tc)
			{
				//list.toarray
				listtc.Add(child.gameObject);

				//set correct tag and layer for each object
				//also ensure all colliders are set to trigger
				child.gameObject.tag = "SimObjPhysics";
				child.gameObject.layer = 8;

				if (child.GetComponent<Collider>())
				{
					child.GetComponent<Collider>().enabled = true;
					child.GetComponent<Collider>().isTrigger = true;
				}

			}

			//MyTriggerColliders = listtc.ToArray();
		}
	}

	// [ContextMenu("Set Up VisibilityPoints")]
	void ContextSetUpVisibilityPoints()
	{
		if (transform.Find("VisibilityPoints"))
		{
			Transform vp = transform.Find("VisibilityPoints");

			List<Transform> vplist = new List<Transform>();

			foreach (Transform child in vp)
			{
				vplist.Add(child);

				//set correct tag and layer for each object
				child.gameObject.tag = "Untagged";
				child.gameObject.layer = 8;
			}

			VisibilityPoints = vplist.ToArray();
		}
	}

	//[ContextMenu("Set Up Rotate Agent Collider")]
	void ContextSetUpBoundingBox()
	{
		if (transform.Find("BoundingBox"))
		{
			BoundingBox = transform.Find("BoundingBox").gameObject;

			//This collider is used as a size reference for the Agent's Rotation checking boxes, so it does not need
			//to be enabled. To ensure this doesn't interact with anything else, set the Tag to Untagged, the layer to 
			//SimObjInvisible, and disable this component. Component values can still be accessed if the component itself
			//is not enabled.
			BoundingBox.tag = "Untagged";
			BoundingBox.layer = 9;//layer 9 - SimObjInvisible

			if (BoundingBox.GetComponent<BoxCollider>())
				BoundingBox.GetComponent<BoxCollider>().enabled = false;
		}
	}
	#endif
}
