using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using System;
using UnityEngine;
using Priority_Queue;

[Serializable]
public class furnitureInstance{
    public string room;
    public string label;
    public float x;
    public float y;
    public float z;
    public float forward_rot; // if a (1) container, used as max x offset; (2) container, used as the radius
    public float max_rot; // if a (1) container, used as max z offset
    public int is_container; // 0: not a container; 1: a container with a rectangular top supporting face where objects can move on; 2: round supporting face; 3: symbolic container
}


// movable nodes including character and item nodes
[Serializable]
public class MovableNode{
    public string name;
    public string label;
    public int nodeType;
    public SerializableVector3 pos;
    public float eulerRot;
    public float headRotOffset;

    public MovableNode(string name, string label, int nodeType){
        this.name = name;
        this.label = label;
        this.nodeType = nodeType;
        pos = new Vector3();
        eulerRot = 0.0f;
        headRotOffset = 0.0f;
    }

    public MovableNode(string name, string label, int nodeType, Vector3 pos, float eulerRot, float headRotOffset = 0.0f){
        this.name = name;
        this.label = label;
        this.nodeType = nodeType;
        this.pos = pos;
        this.eulerRot = eulerRot;
        this.headRotOffset = headRotOffset;
    }
}

// save characters and items pose solutions
[Serializable]
public class LocalSolution{
    public List<MovableNode> nodes;
    public float cost;

    ////////////////// record for debuging //////////////////
    public float individualCost;
    public float individualAlignCost;
    public float individualAcCost;

    public float groupCost;
    
    public float itemCost;
    public float itemOverlapCost;
    public float itemAccCost;
    public float itemAlignCost;
    ////////////////// record for debuging //////////////////

    public LocalSolution(){
        nodes = new List<MovableNode>();
        cost = 0.0f;
    }

    public LocalSolution(LocalSolution other){
        nodes = new List<MovableNode>();
        cost = 0.0f;
        Combine(other);
    }

    public int FindeNodeIdxByName(string name){
        for (int i = 0; i < nodes.Count; i++){
            if (nodes[i].name == name) return i;
        }

        // failed to find the node with given name
        return -1;
    }

    public void AddNode(MovableNode node){
        nodes.Add(node);
    }

    public void Combine(LocalSolution other){
        //cost = other.cost;
        
        foreach(MovableNode node in other.nodes){
            nodes.Add(new MovableNode(node.name, node.label, node.nodeType, node.pos, node.eulerRot, node.headRotOffset));
        }

        cost += other.cost;
        individualCost += other.individualCost;
        individualAlignCost += other.individualAlignCost;
        individualAcCost += other.individualAcCost;
        groupCost += other.groupCost;
        itemCost += other.itemCost;
        itemOverlapCost += other.itemOverlapCost;
        itemAccCost += other.itemAccCost;
        itemAlignCost += other.itemAlignCost;
    }

    // Apply node changess described in other
    public void UpdateFromAnother(LocalSolution other){
        foreach (MovableNode node in other.nodes){
            int nodeIdx = FindeNodeIdxByName(node.name);
            if (nodeIdx != -1){
                nodes[nodeIdx].pos = node.pos;
                nodes[nodeIdx].eulerRot = node.eulerRot;
                nodes[nodeIdx].headRotOffset = node.headRotOffset;
            }
        }

    }

    public float Similarity(LocalSolution other){
        // calculate the cosine similarity between two solutions with the same shape (the number and order of character and item nodes match)
        if (nodes.Count != other.nodes.Count) return 0.0f;
        
        float dotProduct = 0.0f;
        float magnitudeThis = 0.0f;
        float magnitudeOther = 0.0f;
        for (int i = 0; i < nodes.Count; i++){
            if (nodes[i].name != other.nodes[i].name) return 0.0f;

            dotProduct += nodes[i].pos.x * other.nodes[i].pos.x + nodes[i].pos.y * other.nodes[i].pos.y + nodes[i].pos.z * other.nodes[i].pos.z;
            dotProduct += (nodes[i].eulerRot / 180.0f) * (other.nodes[i].eulerRot / 180.0f);
            dotProduct += (nodes[i].headRotOffset / 180.0f) * (other.nodes[i].headRotOffset / 180.0f);

            magnitudeThis += nodes[i].pos.x * nodes[i].pos.x + nodes[i].pos.y * nodes[i].pos.y + nodes[i].pos.z * nodes[i].pos.z;
            magnitudeThis += (nodes[i].eulerRot / 180.0f) * (nodes[i].eulerRot / 180.0f);
            magnitudeThis += (nodes[i].headRotOffset / 180.0f) * (nodes[i].headRotOffset / 180.0f);

            magnitudeOther += other.nodes[i].pos.x * other.nodes[i].pos.x + other.nodes[i].pos.y * other.nodes[i].pos.y + other.nodes[i].pos.z * other.nodes[i].pos.z;
            magnitudeOther += (other.nodes[i].eulerRot / 180.0f) * (other.nodes[i].eulerRot / 180.0f);
            magnitudeOther += (other.nodes[i].headRotOffset / 180.0f) * (other.nodes[i].headRotOffset / 180.0f);
        }

        magnitudeThis = Mathf.Sqrt(magnitudeThis);
        magnitudeOther = Mathf.Sqrt(magnitudeOther);

        return dotProduct / (magnitudeThis * magnitudeOther);
    }

    public float ManhattanDis(LocalSolution other){
        if (nodes.Count != other.nodes.Count) return 100.0f;

        float dis = 0.0f;

        for (int i = 0; i < nodes.Count; i++){
            if (nodes[i].name != other.nodes[i].name) return 100.0f;

            dis += Mathf.Abs(nodes[i].pos.x - other.nodes[i].pos.x); 
            dis += Mathf.Abs(nodes[i].pos.y - other.nodes[i].pos.y); 
            dis += Mathf.Abs(nodes[i].pos.z - other.nodes[i].pos.z);
            dis += Mathf.Abs((nodes[i].eulerRot / 180.0f) - (other.nodes[i].eulerRot / 180.0f));
            dis += Mathf.Abs((nodes[i].headRotOffset / 180.0f) - (other.nodes[i].headRotOffset / 180.0f));
        }

        return dis;
    }

    public void UpdateCost(float weightIndividualActivity, float weightGroupActivity, float weightItemPlacement,
                           Dictionary<string, List<furnitureInstance>> label2furnitureList,
                           List<SpatialGraphNode> characterList, List<SpatialGraphNode> itemList,
                           List<int> individualActivityCharactersIdx,
                           List<int> groupActivityCharactersIdx,
                           Dictionary<string, List<int>> container2itemGroup)
    {
        cost = 0.0f;
        cost += weightIndividualActivity * CalcIndividualActivityCost(label2furnitureList, characterList, itemList, individualActivityCharactersIdx);
        //Debug.Log("inidividual activity cost: " + cost);
        cost += weightGroupActivity * CalcGroupActivityCost(label2furnitureList, characterList, groupActivityCharactersIdx);
        //Debug.Log("group activity cost: " + cost);
        cost += weightItemPlacement * CalcItemPlacementCost(label2furnitureList, itemList, container2itemGroup);
        //Debug.Log("item activity cost: " + cost);
    } 

    public float CalcIndividualActivityCost(Dictionary<string, List<furnitureInstance>> label2furnitureList, List<SpatialGraphNode> characterList, List<SpatialGraphNode> itemList, List<int> individualActivityCharactersIdx){
        float alignmentCost = 0.0f;
        float activityCost = 0.0f;
        float weightAlignmentCost = 0.8f;
        float weightActivityCost = 2.0f;

        //Dictionary<string, List<furnitureInstance>> label2furnitureList = room2label2furnitureList[roomName];
        foreach(int characterIdx in individualActivityCharactersIdx){
            SpatialGraphNode character = characterList[characterIdx];
            if (character.interactVerb == "none") continue;

            string label = character.furnitureNoun;

            // calculate alignment cost
            float rotDif = Mathf.Abs(character.eulerRot - label2furnitureList[label][character.localFurnitureIdx].forward_rot);
            alignmentCost += Mathf.Pow(rotDif / 180.0f, 0.6f);

            // calculate activity cost
            Vector2 characterPos2D = new Vector2(character.pos.x, character.pos.z);
            Vector2 characterForward2D = ForwardDirectionMono.GetForward2D(character.eulerRot + character.headRotOffset);
            Vector2 interactPos2D = new Vector2();
            Vector2 interactForward2D = new Vector2();
            
            // if interact with a furniture object
            if (label2furnitureList.ContainsKey(character.interactNoun)){
                furnitureInstance interactInstance = label2furnitureList[character.interactNoun][0];
                interactPos2D = new Vector2(interactInstance.x, interactInstance.z);
                interactForward2D = ForwardDirectionMono.GetForward2D(interactInstance.forward_rot);
            }
            // else if interact with a virtual item
            else{
                float dis = 1000.0f;
                bool flag = false;
                foreach(SpatialGraphNode item in itemList){
                    if (item.label == character.interactNoun){
                        Vector3 p = item.pos;

                        float tmpDis = Vector3.Distance(character.pos, p);
                        if (tmpDis < dis){
                            dis = tmpDis;
                            interactPos2D = new Vector2(p.x, p.z);
                            interactForward2D = ForwardDirectionMono.GetForward2D(item.eulerRot);

                            flag = true;
                        }
                    }
                }
            }

            Vector2 vecCharacter2Interact = interactPos2D - characterPos2D;

            if (character.interactVerb == "touch"){
                float thresold = 1.0f;
                // if (character.interactNoun == "laptop") thresold = 1.1f;
                float dis2D = Vector2.Distance(characterPos2D, interactPos2D);
                if (dis2D > thresold){
                    //Debug.Log(dis);
                    activityCost += 1.0f * (1.0f - Mathf.Exp(-5.0f * (dis2D - thresold)));
                }
            }
            
            // angle A: character forward vector and the character to interact object vector
            float cosAngleA = Vector2.Dot(vecCharacter2Interact, characterForward2D) / (vecCharacter2Interact.magnitude * characterForward2D.magnitude);
            if (cosAngleA < 0.0f) {
                activityCost += 1.0f;
            }
            else {
                activityCost += 1.0f - Mathf.Pow(cosAngleA, 20);
            }

            //Debug.Log("character at:" + characterGO.transform.position);
            //Debug.Log("interact at:" + interactGO.transform.position);

            // angle B: character forward vector and the inverse interact forward vector
            float cosAngleB = Vector2.Dot(-interactForward2D, characterForward2D) / (interactForward2D.magnitude * characterForward2D.magnitude);

            activityCost += 1.0f - Mathf.Pow(cosAngleB, 3);

            //Debug.Log("cos A: " + cosAngleA + "; cos B: " + cosAngleB + "; cost: " + activityCost);
        }

        this.individualAlignCost = alignmentCost;
        this.individualAcCost = activityCost;
        this.individualCost = weightAlignmentCost * alignmentCost + weightActivityCost * activityCost;

        return weightAlignmentCost * alignmentCost + weightActivityCost * activityCost;
    }

    public float CalcGroupActivityCost(Dictionary<string, List<furnitureInstance>> label2furnitureList, List<SpatialGraphNode> characterList, List<int> groupActivityCharactersIdx){
        float groupActicitycost = 0.0f;

        if (groupActivityCharactersIdx.Count > 1){
            List<SpatialGraphNode> participants = new List<SpatialGraphNode>();

            foreach(int characterIdx in groupActivityCharactersIdx){
                SpatialGraphNode character = characterList[characterIdx];
                participants.Add(character);
            }

            // check if characters' facing directions are towards the polygon inside
            for (int i = 0; i < participants.Count - 1; i++){
                bool flag = CheckFOV.CheckVecFacingPolygonInside(participants, participants[i]);

                if (!flag){
                    //Debug.Log("character " + i + " has no intersects with others.");
                    cost = 2.0f;
                    return cost;
                }
            }

            int[,] attention = new int[participants.Count, participants.Count];
            int paired = 0;

            for (int i = 0; i < participants.Count; i++){
                SpatialGraphNode from = participants[i];
                for (int j = 0; j < participants.Count; j++){
                    if (i == j) continue;

                    SpatialGraphNode to = participants[j];
                    if (CheckFOV.CheckVecInsideFOV(ForwardDirectionMono.GetForward3D(from.eulerRot + from.headRotOffset - 30.0f), 
                                                ForwardDirectionMono.GetForward3D(from.eulerRot + from.headRotOffset + 30.0f),
                                                to.pos - from.pos)) {
                        attention[i, j] = 1;
                        if (attention[j, i] == 1) paired += 1;
                    }
                }
            }

            groupActicitycost = 1.0f - (float)paired / (float)(participants.Count * (participants.Count - 1) / 2);

            for (int i = 0; i < participants.Count; i++){
                float bodyRot = participants[i].eulerRot;
                float headRot = participants[i].headRotOffset;
                if (bodyRot * headRot < 0.0f) groupActicitycost += Mathf.Abs(headRot / 45.0f) / participants.Count;
            }
        }

        this.groupCost = groupActicitycost;
        return groupActicitycost;
    }

    public float CalcItemPlacementCost(Dictionary<string, List<furnitureInstance>> label2furnitureList, List<SpatialGraphNode> itemList, Dictionary<string, List<int>> container2itemGroup){
        float itemPlacementCost = 0.0f;

        this.itemOverlapCost = 0.0f;
        this.itemAccCost = 0.0f;
        this.itemAlignCost = 0.0f;

        //Dictionary<string, List<furnitureInstance>> label2furnitureList = room2label2furnitureList[roomName];
        foreach(string containerName in container2itemGroup.Keys){
            //Debug.Log("containerName: " + containerName);
            furnitureInstance container = label2furnitureList[containerName][0];
            if (container.is_container == 3) continue;

            List<int> group = container2itemGroup[containerName];
            // for (int i = 0; i < group.Count; i++){
            //     SpatialGraphNode itemA = itemList[i];
            //     float minDiffX = 1000.0f;
            //     float minDiffZ = 1000.0f;
            //     bool overlap = false;
            //     for (int j = 0; j < group.Count; j++){
            //         if (i == j) continue;
            //         SpatialGraphNode itemB = itemList[j];

            //         // distance between a pair of items are supposed to be larger than 20cm, otherwise regard as overlapped
            //         if (Vector3.Distance(itemA.pos, itemB.pos) < 0.2f) {
            //             overlap = true;
            //             break;
            //         }
            //         minDiffX = Mathf.Min(minDiffX, Mathf.Abs(itemA.pos.x - itemB.pos.x));
            //         minDiffZ = Mathf.Min(minDiffZ, Mathf.Abs(itemA.pos.z - itemB.pos.z));
            //     }
            //     if (overlap) {
            //         cost = 1.0f * group.Count;
            //         break;
            //     }
            //     else cost += 1.0f - 0.5f * (Mathf.Exp(-20.0f * minDiffX) + Mathf.Exp(-20.0f * minDiffZ));
            // }

            /* part 1: overlap */
            float minPairDis = 1000.0f;
            for (int i = 0; i < group.Count - 1; i++){
                for (int j = i + 1; j < group.Count; j++){
                    float dis = Vector3.Distance(itemList[group[i]].pos, itemList[group[j]].pos);
                    if (dis < minPairDis) minPairDis = dis;
                }
            }
            float cOverlap = 0.0f;
            if (minPairDis <= 0.2f) cOverlap = 1.0f;
            else cOverlap = Mathf.Exp(-20.0f * (minPairDis - 0.2f));

            //if (debug) Debug.Log("Overlap cost = " + cOverlap);
            itemPlacementCost += cOverlap;

            this.itemOverlapCost += cOverlap;

            /* part 2: accessibility */
            float cAcc = 0.0f;

            ////// TEMP: NEED TO ADJUST JSON STRUCTURE LATER //////
            string link = "";
            if (containerName == "tea table container") link = "coach";
            else if (containerName == "dining table container") link = "chair";
            else if (containerName == "cooking platform container") link = "cooking platform";
            else if (containerName == "island container") link = "island";
            else if (containerName == "sink container") link = "sink";
            ////// TEMP: NEED TO ADJUST JSON STRUCTURE LATER //////

            for (int i = 0; i < group.Count; i++){
                // find earest sp
                float minDisToSP = 1000.0f;
                int nearestSPIdx = 0;
                for (int j = 0; j < label2furnitureList[link].Count; j++){
                    float dis = Vector2.Distance(new Vector2(itemList[group[i]].pos.x, itemList[group[i]].pos.z), new Vector2(label2furnitureList[link][j].x, label2furnitureList[link][j].z));

                    if (dis < minDisToSP){
                        minDisToSP = dis;
                        nearestSPIdx = j;
                    }
                }

                if (minDisToSP > 0.3f) {
                    cAcc += 1.0f / group.Count * (1.0f - Mathf.Exp(-5.0f * (minDisToSP - 0.3f)));
                }
                
                Vector2 item2sp = new Vector2(label2furnitureList[link][nearestSPIdx].x, label2furnitureList[link][nearestSPIdx].z) - new Vector2(itemList[group[i]].pos.x, itemList[group[i]].pos.z);
                Vector2 itemForward2D = ForwardDirectionMono.GetForward2D(itemList[group[i]].eulerRot);

                float cos = Vector2.Dot(item2sp, itemForward2D) / (item2sp.magnitude * itemForward2D.magnitude);
                if (cos < 0.707f) cAcc += 1.0f / group.Count;
                else cAcc += (1.0f - Mathf.Pow(cos, 20)) / group.Count;
            }

            //if (debug) Debug.Log("Accessibility cost = " + cAcc);
            itemPlacementCost += cAcc;

            this.itemAccCost += cAcc;

            /* part 3: alignment */
            float cAlign = 0.0f;

            if (group.Count > 1){
                if (container.is_container == 1){
                    for (int i = 0; i < group.Count; i++){
                        float minXDiff = 1000.0f;
                        float minZDiff = 1000.0f;

                        for (int j = 0; j < group.Count; j++){
                            if (i == j) continue;
                            float xDiff = Mathf.Abs(itemList[group[i]].pos.x - itemList[group[j]].pos.x);
                            float zDiff = Mathf.Abs(itemList[group[i]].pos.z - itemList[group[j]].pos.z);
                            if (xDiff < minXDiff) minXDiff = xDiff;
                            if (zDiff < minZDiff) minZDiff = zDiff;
                        }

                        float tmpCost = 0.0f;
                        if (minXDiff <= 0.05f) tmpCost += minXDiff * minXDiff; // diff <= 5cm: cost += diff ^2
                        else tmpCost += 1.0f - Mathf.Exp(0.197496869782f - 4.0f * minXDiff);    // otherwise: cost += 1-e^(0.197496869782 - 4 * diff)
                        if (minZDiff <= 0.05f) tmpCost += minZDiff * minZDiff;
                        else tmpCost += 1.0f - Mathf.Exp(0.197496869782f - 4.0f * minZDiff);

                        cAlign += 0.5f / group.Count * tmpCost;
                    }
                }
                else if (container.is_container == 2){
                    float maxR = 0.0f;
                    float minR = 1000.0f;

                    for (int i = 0; i < group.Count; i++){
                        float r = Vector3.Distance(itemList[group[i]].pos, new Vector3(container.x, container.y, container.z));
                        if (r > maxR) maxR = r;
                        if (r < minR) minR = r;
                    }

                    float diff = maxR - minR;

                    if (diff <= 0.05f) cAlign += diff * diff; // diff <= 5cm: cost += diff ^2
                    else cAlign += 1.0f - Mathf.Exp(0.197496869782f - 4.0f * diff);    // otherwise: cost += 1-e^(0.197496869782 - 4 * diff)
                }
            }

            //if (debug) Debug.Log("Alignment cost = " + cAlign);
            itemPlacementCost += cAlign;

            this.itemAlignCost += cAlign;
        }

        this.itemCost = itemPlacementCost;
        return itemPlacementCost;
    }

    public bool LineLineIntersection(out Vector3 intersection, Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2){
		Vector3 lineVec3 = linePoint2 - linePoint1;
		Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
		Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);
 
		float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);
 
		//is coplanar, and not parrallel
		if(Mathf.Abs(planarFactor) < 0.0001f && crossVec1and2.sqrMagnitude > 0.0001f)
		{
			float s = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
			intersection = linePoint1 + (lineVec1 * s);
			return true;
		}
		else
		{
			intersection = Vector3.zero;
			return false;
		}
	}
}

public class LocalSolutionSampler{
    public string playerCharacterName;
    public float maxTemper;
    public float dec;
    public float maxDeltaRot;
    public float weightIndividualActivity;
    public float weightGroupActivity;
    public float weightItemPlacement;
    
    public Dictionary<string, Dictionary<string, List<furnitureInstance>>> room2label2furnitureList;

    private static float MAX_HEAD_EULER_ROT = 45.0f;

    public LocalSolutionSampler(string playerCharacterName, float maxTemper = 1.0f, float dec = 0.999f){
        this.playerCharacterName = playerCharacterName;
        this.maxTemper = maxTemper;
        this.dec = dec;

        maxDeltaRot = 20.0f;
        weightIndividualActivity = 1.0f;
        weightGroupActivity = 1.0f;
        weightItemPlacement = 1.0f;
    }

    public List<LocalSolution> SampleSGLevelSolutions(SpatialGraph tree, int targetNum){
        List<LocalSolution> solutions = new List<LocalSolution>();
        List<List<LocalSolution>> roomLevelSolutions = new List<List<LocalSolution>>();

        // Debug.Log("Start sampling candidates for " + tree.root.name);

        foreach(SpatialGraphNode room in tree.root.sons){
            if (room.sons.Count > 0){
                //Debug.Log(tree.root.name);
                bool debug = false;
                //if (tree.root.name == "root_84") debug = true;
                List<LocalSolution> roomSolns = SampleRoomLevelSolutions(room, targetNum, debug);
                if (roomSolns.Count > 0) {
                    roomLevelSolutions.Add(roomSolns);
                    // Debug.Log(room.name + ": " + roomSolns.Count);

                }
            }
        }

        List<string> randomCombinations = GenerateRandomCombinations(roomLevelSolutions, targetNum);
        foreach(string comb in randomCombinations){
            string[] indexes = comb.Split(';');
            LocalSolution tmpSolution = new LocalSolution();
            for (int i = 0; i < indexes.Length; i++){
                int idx = int.Parse(indexes[i]);
                tmpSolution.Combine(roomLevelSolutions[i][idx]);
            }

            solutions.Add(tmpSolution);
        }

        return solutions;
    }

    public List<LocalSolution> SampleRoomLevelSolutions(SpatialGraphNode room, int targetNum, bool debug = false){
        List<LocalSolution> solutions = new List<LocalSolution>();
        List<SpatialGraphNode> characterList = new List<SpatialGraphNode>();
        List<SpatialGraphNode> itemList = new List<SpatialGraphNode>();
        Dictionary<string, int> name2characterIdx = new Dictionary<string, int>();
        Dictionary<string, int> name2itemIdx = new Dictionary<string, int>();
        List<int> individualActivityCharactersIdx = new List<int>();
        List<int> groupActivityCharactersIdx = new List<int>();
        Dictionary<string, List<int>> container2itemGroup = new Dictionary<string, List<int>>();

        //Debug.Log("Starting sampling room level solns for " + room.name);

        // preprocess all nodes using a stack
        Stack<SpatialGraphNode> stack = new Stack<SpatialGraphNode>();
        stack.Push(room);
        bool valid = false;
        while(stack.Count != 0){
            var top = stack.Pop();
            if (top.nodeType == 3) {
                characterList.Add(top);
                name2characterIdx.Add(top.name, characterList.Count - 1);
                if (top.interactVerb == "chat") groupActivityCharactersIdx.Add(characterList.Count - 1);
                else {
                    if (top.interactVerb != "get" && top.interactVerb != "put") individualActivityCharactersIdx.Add(characterList.Count - 1);
                }

                valid = true;
            }
            else if (top.nodeType == 4) {
                if (top.father.nodeType == 3) continue; // if the item is currently carried by characters

                string label = top.father.label + " container";
                if (!container2itemGroup.ContainsKey(label)) container2itemGroup.Add(label, new List<int>());
                itemList.Add(top);
                name2itemIdx.Add(top.name, itemList.Count - 1);
                container2itemGroup[label].Add(itemList.Count - 1);

                valid = true;
            }

            foreach (var son in top.sons) stack.Push(son);
        }

        if (!valid) return solutions;

        List<LocalSolution> saved = new List<LocalSolution>();
        //SimplePriorityQueue<LocalSolution> pq = new SimplePriorityQueue<LocalSolution>();
        int rejectCnt = 0;

        // sample the target num of solutions
        // for (int i = 0; i < targetNum * 2; i++){
        for (int i = 0; i < targetNum; i++){
            LocalSolution soln = MCMCRoomLevelSolution(characterList, itemList, name2characterIdx, name2itemIdx,
                                                       individualActivityCharactersIdx, groupActivityCharactersIdx,
                                                       container2itemGroup, room2label2furnitureList[room.name], debug);

            // add the sampled solution into the pool, if it is not too similar to the other saved solutions
            bool flag = true;
            float manhattanDisThreshold = 0.02f;
            foreach (LocalSolution otherSoln in saved){
                if (soln.ManhattanDis(otherSoln) < manhattanDisThreshold){
                    flag = false;
                    rejectCnt++;
                    break;
                }
            }
            if (flag){
                saved.Add(soln);
                //pq.Enqueue(soln, soln.cost);
                solutions.Add(soln);
                rejectCnt = 0;
            }
            else{
                if (rejectCnt == 5) break;
            }
        }  

        // while (pq.Count != 0 && solutions.Count < targetNum){
        //     solutions.Add(pq.Dequeue());
        // }
        return solutions;
    }

    LocalSolution MCMCRoomLevelSolution(List<SpatialGraphNode> characterList, List<SpatialGraphNode> itemList,
                                        Dictionary<string, int> name2characterIdx, Dictionary<string, int> name2itemIdx,
                                        List<int> individualActivityCharactersIdx, List<int> groupActivityCharactersIdx,
                                        Dictionary<string, List<int>> container2itemGroup, Dictionary<string, List<furnitureInstance>> label2furnitureList, bool debug = false){
        //if (debug) Debug.Log("mcmc room starts...");
        
        // init characters' poses
        Dictionary<string, List<string>> occupancy = new Dictionary<string, List<string>>();
        foreach(SpatialGraphNode character in characterList){
            string label = character.furnitureNoun;
            //if (debug) Debug.Log(character.name + ": " + character.furnitureNoun + ", " + label2furnitureList[label].Count + " possible slots");
            if (!occupancy.ContainsKey(label)){
                occupancy.Add(label, new List<string>());
                for (int i = 0; i < label2furnitureList[label].Count; i++){
                    occupancy[label].Add("");
                }
            }

            while(true){
                int furnitureIdx = UnityEngine.Random.Range(0, occupancy[label].Count);
                if (occupancy[label][furnitureIdx] == ""){
                    occupancy[label][furnitureIdx] = character.name;
                    character.localFurnitureIdx = furnitureIdx;
                    character.pos = new Vector3(label2furnitureList[label][furnitureIdx].x, label2furnitureList[label][furnitureIdx].y, label2furnitureList[label][furnitureIdx].z);
                    character.eulerRot = label2furnitureList[label][furnitureIdx].forward_rot;
                    break;
                }
            }
        }

        // init items' poses
        foreach(SpatialGraphNode item in itemList){
            string label = item.father.label + " container";
            item.pos = new Vector3(label2furnitureList[label][0].x, label2furnitureList[label][0].y, label2furnitureList[label][0].z);
            item.eulerRot = label2furnitureList[label][0].forward_rot;
        }

        //if (debug) Debug.Log("characters and items initialized.");

        // init solution
        LocalSolution soln = new LocalSolution();
        foreach(SpatialGraphNode character in characterList) soln.AddNode(new MovableNode(character.name, character.label, 3, character.pos, character.eulerRot, character.headRotOffset));
        foreach(SpatialGraphNode item in itemList) soln.AddNode(new MovableNode(item.name, item.label, 4, item.pos, item.eulerRot));
        //if (debug) Debug.Log("calculating initial cost...");
        soln.UpdateCost(weightIndividualActivity, weightGroupActivity, weightItemPlacement, label2furnitureList,
                        characterList, itemList, individualActivityCharactersIdx, groupActivityCharactersIdx, container2itemGroup);

        //if (debug) Debug.Log("initial cost done.");

        // MCMC sampling; put solutions into a priority queue and add the top solutions into the pool
        float temper = maxTemper;

        while(temper > 0.01f){
            Stack<SpatialGraphNode> stack = new Stack<SpatialGraphNode>(); // use the stack to save proposed moves; if the proposal is declined, rollback the moves
            LocalSolution proposal = new LocalSolution(soln);
            float prob;
            
            // MOVE 1: select a character node and apply a random move
            if (characterList.Count > 0){
                int toMoveCharacterIdx = UnityEngine.Random.Range(0, characterList.Count);
                int playerCharacterIdx = -1;
                if (name2characterIdx.ContainsKey(playerCharacterName)) playerCharacterIdx = name2characterIdx[playerCharacterName];
                SpatialGraphNode toMoveCharacter = characterList[toMoveCharacterIdx];
                string label = toMoveCharacter.furnitureNoun;
                stack.Push(toMoveCharacter.CopyNode(false));

                prob = UnityEngine.Random.Range(0.0f, 1.0f);
                float characterTranslationProb = 0.3f;
                if (temper >= 0.5f && temper < 1.0f) characterTranslationProb = 0.2f;
                else if (temper >= 0.2f && temper < 0.5f) characterTranslationProb = 0.15f;
                else if (temper >= 0.1f && temper < 0.2f) characterTranslationProb = 0.05f;
                else if (temper >= 0.05f && temper < 0.1f) characterTranslationProb = 0.01f;
                else if (temper < 0.05f) characterTranslationProb = 0.005f;

                if (toMoveCharacterIdx == playerCharacterIdx) characterTranslationProb = 0.0f;

                // translate
                if (prob <= characterTranslationProb){
                    int furnitureIdx = UnityEngine.Random.Range(0, occupancy[label].Count);

                    // if the sampled furniture obj is the one the character is occupying now, change the action to rotate
                    if (furnitureIdx == toMoveCharacter.localFurnitureIdx){
                        prob = 1.0f;
                    }
                    else{
                        // if no one else occupying the selected furniture
                        if (occupancy[label][furnitureIdx] == ""){
                            occupancy[label][toMoveCharacter.localFurnitureIdx] = "";

                            float curRotOffset = toMoveCharacter.eulerRot - label2furnitureList[label][toMoveCharacter.localFurnitureIdx].forward_rot;

                            toMoveCharacter.pos = new Vector3(label2furnitureList[label][furnitureIdx].x, label2furnitureList[label][furnitureIdx].y, label2furnitureList[label][furnitureIdx].z);
                            if (curRotOffset > label2furnitureList[label][furnitureIdx].max_rot) curRotOffset = 0.0f;
                            toMoveCharacter.eulerRot = label2furnitureList[label][furnitureIdx].forward_rot + curRotOffset;
                            toMoveCharacter.headRotOffset = 0.0f;
                        }
                        // if there is already someone, swap them
                        else{ 
                            SpatialGraphNode otherCharacter = characterList[name2characterIdx[occupancy[label][furnitureIdx]]];
                            stack.Push(otherCharacter.CopyNode(false));

                            occupancy[label][toMoveCharacter.localFurnitureIdx] = otherCharacter.name;
                            otherCharacter.localFurnitureIdx = toMoveCharacter.localFurnitureIdx;

                            Vector3 tmpPos = toMoveCharacter.pos;
                            float tmpRot = toMoveCharacter.eulerRot;
                            toMoveCharacter.pos = otherCharacter.pos;
                            toMoveCharacter.eulerRot = otherCharacter.eulerRot;
                            otherCharacter.pos = tmpPos;
                            otherCharacter.eulerRot = tmpRot;

                            proposal.nodes[proposal.FindeNodeIdxByName(otherCharacter.name)].pos = otherCharacter.pos;
                            proposal.nodes[proposal.FindeNodeIdxByName(otherCharacter.name)].eulerRot = otherCharacter.eulerRot;
                        }

                        //Debug.Log("translate from " + label + " " + toMoveCharacter.localFurnitureIdx + " to " + furnitureIdx);
                        occupancy[label][furnitureIdx] = toMoveCharacter.name;
                        toMoveCharacter.localFurnitureIdx = furnitureIdx;
                    }
                }

                // rotate
                if (prob > characterTranslationProb) {
                    float deltaRot = 0.0f;
                    float curRotOffset = toMoveCharacter.eulerRot - label2furnitureList[label][toMoveCharacter.localFurnitureIdx].forward_rot;

                    float rotBodyProb = UnityEngine.Random.Range(0.0f, 1.0f);

                    if (rotBodyProb <= 0.5f && label2furnitureList[label][toMoveCharacter.localFurnitureIdx].max_rot >= 1.0f){  //rotate body by a delta
                        while(true){
                            deltaRot = UnityEngine.Random.Range(-maxDeltaRot, maxDeltaRot);
                            if (Mathf.Abs(curRotOffset + deltaRot) < label2furnitureList[label][toMoveCharacter.localFurnitureIdx].max_rot) break;
                        }

                        toMoveCharacter.eulerRot += deltaRot;
                    }
                    else{   //rotate head by a delta
                        while(true){
                            deltaRot = UnityEngine.Random.Range(-maxDeltaRot, maxDeltaRot);
                            if (Mathf.Abs(toMoveCharacter.headRotOffset + deltaRot) < MAX_HEAD_EULER_ROT) break;
                        }

                        toMoveCharacter.headRotOffset += deltaRot;
                    }
                }

                proposal.nodes[proposal.FindeNodeIdxByName(toMoveCharacter.name)].pos = toMoveCharacter.pos;
                proposal.nodes[proposal.FindeNodeIdxByName(toMoveCharacter.name)].eulerRot = toMoveCharacter.eulerRot;
                proposal.nodes[proposal.FindeNodeIdxByName(toMoveCharacter.name)].headRotOffset = toMoveCharacter.headRotOffset;
            }

            // MOVE 2: select an item node and apply a random move
            if (itemList.Count > 0) {
                int toMoveItemIdx = UnityEngine.Random.Range(0, itemList.Count);
                SpatialGraphNode toMoveItem = itemList[toMoveItemIdx];
                string label = toMoveItem.father.label + " container";
                stack.Push(toMoveItem.CopyNode(false));

                prob = UnityEngine.Random.Range(0.0f, 1.0f);

                // translate
                if (prob <= 0.5f){
                    float maxDeltaMove = 0.3f;
                    if (temper >= 0.1f && temper < 0.2f) maxDeltaMove = 0.2f;
                    else if (temper < 0.1f) maxDeltaMove = 0.1f;

                    float deltaX = 0.0f;
                    float deltaZ = 0.0f;
                    float curXOffset = toMoveItem.pos.x - label2furnitureList[label][0].x;
                    float curZOffset = toMoveItem.pos.z - label2furnitureList[label][0].z;

                    if (label2furnitureList[label][0].is_container == 1){
                        while(true){
                            deltaX = UnityEngine.Random.Range(-maxDeltaMove, maxDeltaMove);
                            if (Mathf.Abs(curXOffset + deltaX) < label2furnitureList[label][0].forward_rot) break;
                        }
                        while(true){
                            deltaZ = UnityEngine.Random.Range(-maxDeltaMove, maxDeltaMove);
                            if (Mathf.Abs(curZOffset + deltaZ) < label2furnitureList[label][0].max_rot) break;
                        }
                    }
                    else if (label2furnitureList[label][0].is_container == 2){
                        while(true){
                            deltaX = UnityEngine.Random.Range(-maxDeltaMove, maxDeltaMove);
                            deltaZ = UnityEngine.Random.Range(-maxDeltaMove, maxDeltaMove);
                            if (Mathf.Pow(curXOffset + deltaX, 2) + Mathf.Pow(curZOffset + deltaZ, 2) < Mathf.Pow(label2furnitureList[label][0].forward_rot, 2)) break;
                        }
                    }

                    toMoveItem.pos.x += deltaX;
                    toMoveItem.pos.z += deltaZ;
                }

                // rotate
                else{
                    float deltaRot = UnityEngine.Random.Range(-maxDeltaRot, maxDeltaRot);
                    toMoveItem.eulerRot += deltaRot;
                }

                proposal.nodes[proposal.FindeNodeIdxByName(toMoveItem.name)].pos = toMoveItem.pos;
                proposal.nodes[proposal.FindeNodeIdxByName(toMoveItem.name)].eulerRot = toMoveItem.eulerRot;

                if (toMoveItem.label == "dish") {
                    //Debug.Log("yes this is a dish, moved to: " + toMoveItem.pos + "; " + toMoveItem.eulerRot);
                    //Debug.Log("Really? It is: " + proposal.nodes[proposal.FindeNodeIdxByName(toMoveItem.name)].pos + "; " + proposal.nodes[proposal.FindeNodeIdxByName(toMoveItem.name)].eulerRot);
                }
            }

            // update cost and decide whether to accept the proposal. If not, rollback moved nodes
            proposal.UpdateCost(weightIndividualActivity, weightGroupActivity, weightItemPlacement, label2furnitureList,
                                characterList, itemList, individualActivityCharactersIdx, groupActivityCharactersIdx, container2itemGroup);


            float acceptProb = Mathf.Exp((soln.cost - proposal.cost) / temper);
            acceptProb = Mathf.Min(1.0f, acceptProb);
            prob = UnityEngine.Random.Range(0.0f, 1.0f);

            //Debug.Log("original cost: " + soln.cost + "; proposal cost: " + proposal.cost);

            if (prob < acceptProb) {
                soln = proposal;

                //Debug.Log("proposal accepted!!!!!!!!!!!!!!!!!!!!\n////////////////////////////////////");
            }
            else{
                // if the proposal is declined, rollback nodes saved in the stack
                Stack<SpatialGraphNode> rollbackStack = new Stack<SpatialGraphNode>();

                // first, clear the occupancy flag; add the node into the rollback stack
                while(stack.Count != 0){
                    var top = stack.Pop();
                    if (top.nodeType == 3) {
                        int rollbackCharacterIdx = name2characterIdx[top.name];
                        string label = characterList[rollbackCharacterIdx].furnitureNoun;
                        occupancy[label][characterList[rollbackCharacterIdx].localFurnitureIdx] = "";
                    }
                    rollbackStack.Push(top);
                }
                // next, roll back nodes' info
                while(rollbackStack.Count != 0){
                    var top = rollbackStack.Pop();
                    if (top.nodeType == 3) {
                        int rollbackCharacterIdx = name2characterIdx[top.name];
                        string label = characterList[rollbackCharacterIdx].furnitureNoun;
                        characterList[rollbackCharacterIdx].pos = top.pos;
                        characterList[rollbackCharacterIdx].eulerRot = top.eulerRot;
                        characterList[rollbackCharacterIdx].headRotOffset = top.headRotOffset;
                        characterList[rollbackCharacterIdx].localFurnitureIdx = top.localFurnitureIdx;
                        occupancy[label][top.localFurnitureIdx] = top.name;
                    }
                    else if (top.nodeType == 4) {
                        int rollbackItemIdx = name2itemIdx[top.name];
                        itemList[rollbackItemIdx].pos = top.pos;
                        itemList[rollbackItemIdx].eulerRot = top.eulerRot;
                    }
                }

                //Debug.Log("proposal declined.\n////////////////////////////////////");
            }

            temper *= dec;
        }

        return soln;
    }

    List<string> GenerateRandomCombinations(List<List<LocalSolution>> roomLevelSolutions, int targetNum){
        int numRooms = roomLevelSolutions.Count;
        int sum = 1;
        List<int> cnt = new List<int>();
        for (int i = 0; i < numRooms; i++){
            sum *= roomLevelSolutions[i].Count;
            cnt.Add(roomLevelSolutions[i].Count);
        }
        if (sum < targetNum) targetNum = sum;

        List<string> allCombinations = new List<string>();
        GenerateAllCombinations(cnt, 0, "", allCombinations);
        allCombinations.Shuffle();

        List<string> randomCombinations = new List<string>();
        for (int i = 0; i < targetNum; i++) randomCombinations.Add(allCombinations[i]);
        return randomCombinations;
    }

    void GenerateAllCombinations(List<int> cnt, int idx, string prefix, List<string> allCombinations){
        for (int i = 0; i < cnt[idx]; i++){
            string tmp = prefix + i;
            if (idx == cnt.Count - 1) allCombinations.Add(tmp);
            else {
                tmp += ';';
                GenerateAllCombinations(cnt, idx + 1, tmp, allCombinations);
            }
        }
    }
}

public class StorySolutionSampler{
    public string playerCharacterName;
    public float maxTemper;
    public float dec;
    public float weightIndividualActivity;
    public float weightTrajectory;

    public ScriptParser parser;
    //public ShortestPath sp;
    public LocalSolutionSampler localSampler;
    public List<EventGraph> eventGraphList;
    public List<string> characterNames;
    public List<string> itemNames;
    public List<(int, int)> story; // (int: sg branch index, int: candidate solution index)
    public Dictionary<string, Dictionary<string, List<furnitureInstance>>> room2label2furnitureList;
    
    public StorySolutionSampler(string playerCharacterName, string jsonFName, float maxTemper = 0.1f, float dec = 0.999f){
        this.playerCharacterName = playerCharacterName;
        this.maxTemper = maxTemper;
        this.dec = dec;

        weightIndividualActivity = 1.0f;
        weightTrajectory = 1.0f;

        story = new List<(int, int)>();
        LoadSceneFurnitureInstances(jsonFName);
    }

    public void LoadSceneFurnitureInstances(string fName){
        //string jsonString  = System.IO.File.ReadAllText("json/chinese_furniture.json");
        string jsonString  = System.IO.File.ReadAllText("json/" + fName);  
        //Debug.Log(jsonString);
        furnitureInstance[] furnitureInstanceList = JsonHelper.FromJson<furnitureInstance>(jsonString);

        room2label2furnitureList = new Dictionary<string, Dictionary<string, List<furnitureInstance>>>(); 
        foreach (furnitureInstance instance in furnitureInstanceList){
            string roomName = instance.room;
            string furnitureLabel = instance.label;
            if (instance.is_container > 0) furnitureLabel += " container";

            if (!room2label2furnitureList.ContainsKey(roomName)) room2label2furnitureList.Add(roomName, new Dictionary<string, List<furnitureInstance>>());
            if (!room2label2furnitureList[roomName].ContainsKey(furnitureLabel)) room2label2furnitureList[roomName].Add(furnitureLabel, new List<furnitureInstance>());
            room2label2furnitureList[roomName][furnitureLabel].Add(instance);
            //Debug.Log(instance.label + ", " + instance.room + ", " + instance.is_container);
        }
    }

    public void PrepareEventGraphs(string storyFile = "testv5.txt"){
        localSampler = new LocalSolutionSampler(playerCharacterName, 1.0f);
        localSampler.room2label2furnitureList = room2label2furnitureList;

        parser = new ScriptParser(storyFile, room2label2furnitureList);
        eventGraphList = parser.eventGraphList;

        // Debug.Log("Event graphs finished.");

        characterNames = eventGraphList[0].spatialGraphCandidates[0].name2character.Keys.ToList();
        itemNames = eventGraphList[0].spatialGraphCandidates[0].name2item.Keys.ToList();

        float lastInterval = Time.realtimeSinceStartup;
        foreach (var eg in eventGraphList){
            foreach (var sg in eg.spatialGraphCandidates){
                sg.candidates = localSampler.SampleSGLevelSolutions(sg, 50);
                sg.BuildCandidatesKNearestNeighbors(10);
                // Debug.Log(sg.root.name + " total: " + sg.candidates.Count);
            }
        }
        float timeNow = Time.realtimeSinceStartup;
        // Debug.Log(" It took " + (timeNow - lastInterval) + " to sample all candidates.");
        lastInterval = timeNow;

        IFormatter formatter = new BinaryFormatter();
        Stream stream = new FileStream("SaveTest/" + storyFile, FileMode.Create, FileAccess.Write);
        formatter.Serialize(stream, eventGraphList);
        stream.Close();
    }

    public void LoadEventGraphs(string fName){
        IFormatter formatter = new BinaryFormatter();
        Stream stream = new FileStream("SaveTest/" + fName, FileMode.Open,FileAccess.Read);
        eventGraphList = (List<EventGraph>)formatter.Deserialize(stream);
        stream.Close();

        characterNames = eventGraphList[0].spatialGraphCandidates[0].name2character.Keys.ToList();
        itemNames = eventGraphList[0].spatialGraphCandidates[0].name2item.Keys.ToList();

        /////////////////////////////////////////////
        // foreach (var eg in eventGraphList){
        //     if (eg.spatialGraphCandidates[0].root.name == "root_20"){
        //         foreach (var candidate in eg.spatialGraphCandidates[0].candidates){
        //             string buffer = "";
        //             foreach (var node in candidate.nodes){
        //                 buffer += node.name + ", " + node.pos + ", " + node.eulerRot + "," + node.headRotOffset + "; ";
        //             }
        //             Debug.Log(buffer);
        //         }
        //     }
        // }
    }

    List<LocalSolution> InstantiateStory(List<(int, int)> solutionsIdx){
        List<LocalSolution> PGs = new List<LocalSolution>();

        for (int i = 0; i < eventGraphList.Count; i++){
            (int, int) solnIdx = (0, 0);
            if (i < story.Count) solnIdx = story[i];
            else solnIdx = solutionsIdx[i - story.Count];

            SpatialGraph sg = eventGraphList[i].spatialGraphCandidates[solnIdx.Item1];
            LocalSolution candidateSoln = sg.candidates[solnIdx.Item2];

            if (i == 0) PGs.Add(candidateSoln);
            else {
                // a pg should inherit unchanged nodes from the previous event, and include newly changed nodes in the current event
                LocalSolution pg = new LocalSolution(PGs[PGs.Count - 1]);
                pg.UpdateFromAnother(candidateSoln);
                PGs.Add(pg);
            }
        }

        return PGs;
    }

    float CalcStroyCost(List<LocalSolution> PGs){
        float cost = 0.0f;

        for (int i = 0; i < PGs.Count; i++){
            LocalSolution pg = PGs[i];

            // calculate trajectory legth cost
            if (i > 0){     // frame 0 is the start frame
                foreach (string characterName in characterNames){
                    MovableNode character = pg.nodes[pg.FindeNodeIdxByName(characterName)];
                    MovableNode characterLastFrame = PGs[i - 1].nodes[PGs[i - 1].FindeNodeIdxByName(characterName)];
                    
                    if (((Vector3)character.pos - (Vector3)characterLastFrame.pos).magnitude > 0.1f){
                        // TODO: update the shortest path from precomputed results to speed-up
                        //cost += sp.FindShortestPath(lastPos, character.pos).Count / 100.0f;
                    }
                }
            }

            // sum spatial costs
            cost += pg.cost;
        }

        return cost;
    }

    public Dictionary<string, (int, int)> DymanicStory(int curEGIdx){ 
        Dictionary<string, (int, int)> ret = new Dictionary<string, (int, int)>();
        HashSet<string> possibleFurnitureNouns = new HashSet<string>();
        List<furnitureInstance> possibleFurnitureInstances = new List<furnitureInstance>();
        EventGraph eg = eventGraphList[curEGIdx];
        string playCharacterRoomName = "";

        float lastInterval = Time.realtimeSinceStartup;

        // player should participate in this event
        if (eg.spatialGraphCandidates[0].name2character.ContainsKey(playerCharacterName)){
            foreach (var sg in eg.spatialGraphCandidates){
                SpatialGraphNode character = sg.name2character[playerCharacterName];
                possibleFurnitureNouns.Add(character.furnitureNoun);

                if (playCharacterRoomName == "") playCharacterRoomName = character.father.father.name;
            }

            foreach (var furnitureNoun in possibleFurnitureNouns){
                if (!room2label2furnitureList.ContainsKey(playCharacterRoomName)){
                    // Debug.Log(playCharacterRoomName + " not exists.");
                }
                if (!room2label2furnitureList[playCharacterRoomName].ContainsKey(furnitureNoun)){
                    // Debug.Log(playCharacterRoomName + ", " + furnitureNoun + " not exists.");
                }
                foreach (var furnitureInstance in room2label2furnitureList[playCharacterRoomName][furnitureNoun]){
                    possibleFurnitureInstances.Add(furnitureInstance);
                }
            }

            float timeNow = Time.realtimeSinceStartup;
            // Debug.Log(" It took " + (timeNow - lastInterval) + " to prepare pull possible furniture nouns.");

            // each furniture instance corresponds to a user action
            // synthesize a story for each action
            foreach (var furnitureInstance in possibleFurnitureInstances){ 
                lastInterval = timeNow;

                Vector2 playerCharacterPos = new Vector2(furnitureInstance.x, furnitureInstance.z);

                //Debug.Log(furnitureInstance.label + " at " + playerCharacterPos);

                float totalProb = 0.0f;

                // build candidate masks to temporarily disable candidate solutions that do not fit the current user action
                List<int> bannedSGIdxes = new List<int>();
                //foreach (var sg in eg.spatialGraphCandidates){
                for (int i = 0; i < eg.spatialGraphCandidates.Count; i++){
                    SpatialGraph sg = eg.spatialGraphCandidates[i];
                    sg.candidatesMasks = new List<bool>();
                    bool hasActiveCandidates = false;
                    foreach (var candidateSoln in sg.candidates){
                        bool mask = false;
                        Vector3 thisPlayerCharacterPosVec3 = candidateSoln.nodes[candidateSoln.FindeNodeIdxByName(playerCharacterName)].pos;
                        Vector2 thisPlayerCharacterPos = new Vector2(thisPlayerCharacterPosVec3.x, thisPlayerCharacterPosVec3.z);
                        if (Vector2.Distance(playerCharacterPos, thisPlayerCharacterPos) < 0.001f) {
                            mask = true;
                            hasActiveCandidates = true;
                        }
                        sg.candidatesMasks.Add(mask);
                    }

                    if (hasActiveCandidates){
                        totalProb += sg.prob;
                        sg.BuildCandidatesKNearestNeighbors(10);
                    }
                    else bannedSGIdxes.Add(i);
                }

                // update the probability model on SGs
                if (1.0f - totalProb > 1e-7){
                    float scale = 1.0f / totalProb;

                    for (int i = 0; i < eg.spatialGraphCandidates.Count; i++){
                        //Debug.Log(eg.spatialGraphCandidates[i].prob);
                        if (bannedSGIdxes.Contains(i)) eg.spatialGraphCandidates[i].dynamicProb = 0.0f;
                        else eg.spatialGraphCandidates[i].dynamicProb = eg.spatialGraphCandidates[i].prob * scale;
                    }
                }

                timeNow = Time.realtimeSinceStartup;
                // Debug.Log(" It took " + (timeNow - lastInterval) + " to generate candidate masks and update probs.");
                lastInterval = timeNow;
                
                // synthesize a whole story based on the current user action, but only keep the solution for the current event
                List<(int, int)> solutionsIdxes = MCMCStoryLevelSolution(curEGIdx);
                timeNow = Time.realtimeSinceStartup;
                // Debug.Log(" It took " + (timeNow - lastInterval) + " to try sampling.");

                // TODO: save: eg.spatialGraphCandidates[solutionsIdxes[0].Item1].candidates[solutionsIdxes[0].Item2];
                ret.Add(playerCharacterPos.ToString(), solutionsIdxes[curEGIdx]);
            }
        }

        // player not in this event
        else{
            // build candidate masks to temporarily disable candidate solutions that do not fit the current user action
            List<int> bannedSGIdxes = new List<int>();
            //foreach (var sg in eg.spatialGraphCandidates){
            for (int i = 0; i < eg.spatialGraphCandidates.Count; i++){
                SpatialGraph sg = eg.spatialGraphCandidates[i];
                sg.candidatesMasks = new List<bool>();
                foreach (var candidateSoln in sg.candidates){
                    sg.candidatesMasks.Add(true);
                }

                sg.BuildCandidatesKNearestNeighbors(10);
            }

            // update the probability model on SGs
            for (int i = 0; i < eg.spatialGraphCandidates.Count; i++){
                //Debug.Log(eg.spatialGraphCandidates[i].prob);
                eg.spatialGraphCandidates[i].dynamicProb = eg.spatialGraphCandidates[i].prob;
            }

            float timeNow = Time.realtimeSinceStartup;
            // Debug.Log(" It took " + (timeNow - lastInterval) + " to generate candidate masks and update probs.");
            lastInterval = timeNow;
            
            // synthesize a whole story based on the current user action, but only keep the solution for the current event
            List<(int, int)> solutionsIdxes = MCMCStoryLevelSolution(curEGIdx);
            timeNow = Time.realtimeSinceStartup;
            // Debug.Log(" It took " + (timeNow - lastInterval) + " to try sampling.");

            ret.Add("dummy", solutionsIdxes[curEGIdx]);
        }

        return ret;
    }

    public List<(int, int)> MCMCStoryLevelSolution(int curEGIdx){
        float lastInterval = Time.realtimeSinceStartup;
        
        // init solution
        List<(int, int)> solutionsIdxes = new List<(int, int)>(); // (int: sg branch index, int: candidate solution index)

        /////////////// TEMP /////////////////
        for (int i = 0; i < curEGIdx; i++) solutionsIdxes.Add((0, 0)); // dummy
        for (int i = curEGIdx; i < eventGraphList.Count; i++){
            EventGraph eg = eventGraphList[i];

            // step 1: draw a random prob, select a branch of sg          
            int sgIdx = eg.RandomSGBranchIdx();
            SpatialGraph sg = eg.spatialGraphCandidates[sgIdx];

            // step 2: draw a random candidate solution with its mask true
            int candidateIdx = -1;
            if (i == curEGIdx){ // masks are only created for SGs under the current event
                List<int> activeCandidateIdxes = new List<int>();
                for (int j = 0; j < sg.candidates.Count; j++){
                    if (sg.candidatesMasks[j]) activeCandidateIdxes.Add(j);
                }
                candidateIdx = activeCandidateIdxes[UnityEngine.Random.Range(0, activeCandidateIdxes.Count)];
            }
            else candidateIdx = UnityEngine.Random.Range(0, sg.candidates.Count);

            solutionsIdxes.Add((sgIdx, candidateIdx));
        }
        /////////////// TEMP /////////////////

        // Debug.Log("soln inited");
        float timeNow = Time.realtimeSinceStartup;
        //Debug.Log(" It took " + (timeNow - lastInterval) + " to init the soln.");
        lastInterval = timeNow;

        float cost = CalcStroyCost(InstantiateStory(solutionsIdxes));
        
        float temper = maxTemper;
        // Debug.Log("init temp: " + temper);

        timeNow = Time.realtimeSinceStartup;
        //Debug.Log(" It took " + (timeNow - lastInterval) + " to calc the init soln cost.");

        int iterCnt = 0;
        while(temper > 0.01f){
            lastInterval = timeNow;

            // pick an EG to make a move, sample the move from the neighborhood of the current solution

            ////////////// Since dummy indexes included, starting from curEGIdx ///////////////////
            int toMoveEGIdx = UnityEngine.Random.Range(curEGIdx, solutionsIdxes.Count);
            ////////////// Since dummy indexes included, starting from curEGIdx ///////////////////

            (int, int) currentSolnIdx = solutionsIdxes[toMoveEGIdx];
            (int, int) proposedSolnIdx = (0, 0);

            float SGBranchSwitchProb = 0.1f;
            if (temper >= 0.2f && temper < 0.5f) SGBranchSwitchProb = 0.05f;
            else if (temper >= 0.1f && temper < 0.2f) SGBranchSwitchProb = 0.02f;
            else if (temper >= 0.05f && temper < 0.1f) SGBranchSwitchProb = 0.01f;
            else if (temper < 0.05f) SGBranchSwitchProb = 0.001f;

            if (UnityEngine.Random.Range(0.0f, 1.0f) < SGBranchSwitchProb){ // switch SG branch, and randomly pick a candidate solution
                // Debug.Log("switch a branch to make a proposal");
                EventGraph eg = eventGraphList[toMoveEGIdx];

                // step 1: draw a random prob, select a branch of sg          
                int sgIdx = eg.RandomSGBranchIdx();
                SpatialGraph sg = eg.spatialGraphCandidates[sgIdx];

                // step 2: draw a random candidate solution with its mask true
                int candidateIdx = -1;
                if (toMoveEGIdx == curEGIdx){ // masks are only created for SGs under the current event
                    List<int> activeCandidateIdxes = new List<int>();
                    for (int j = 0; j < sg.candidates.Count; j++){
                        if (sg.candidatesMasks[j]) activeCandidateIdxes.Add(j);
                    }
                    candidateIdx = activeCandidateIdxes[UnityEngine.Random.Range(0, activeCandidateIdxes.Count)];
                }
                else candidateIdx = UnityEngine.Random.Range(0, sg.candidates.Count);

                proposedSolnIdx = (sgIdx, candidateIdx);
            }
            else{   // keep the same SG branch, and propose to select another candidate solution
                // Debug.Log("candidate jump to make a proposal");
                SpatialGraph sg = eventGraphList[toMoveEGIdx].spatialGraphCandidates[currentSolnIdx.Item1];

                List<int> targets = sg.nearestNeighbors[currentSolnIdx.Item2].ToList();
                int targetSize = targets.Count;
                if (temper >= 0.5f && temper < 1.0f) targetSize = targetSize * 2;
                else if (temper >= 0.2f && temper < 0.5f) targetSize = (int)(targetSize * 1.5);
                else if (temper >= 0.1f && temper < 0.2f) targetSize = (int)(targetSize * 1.2);
                else if (temper >= 0.05f && temper < 0.1f) targetSize = (int)(targetSize * 1.1);

                List<int> outsideNeighborhoodCandidates = new List<int>();
                for (int anotherCandidateIdx = 0; anotherCandidateIdx < sg.candidates.Count; anotherCandidateIdx++){
                    if (anotherCandidateIdx != currentSolnIdx.Item2 && 
                        (sg.candidatesMasks == null || sg.candidatesMasks[anotherCandidateIdx]) && 
                        !targets.Contains(anotherCandidateIdx)) {
                        outsideNeighborhoodCandidates.Add(anotherCandidateIdx);
                    }
                }

                outsideNeighborhoodCandidates.Shuffle();

                foreach (int anotherCandidateIdx in outsideNeighborhoodCandidates){
                    if (targets.Count < targetSize) targets.Add(anotherCandidateIdx);
                    else break;
                }

                /////////////////////////////////////////////////////////////////////////////////////
                // if (targets.Count == 0){
                //     Debug.Log(sg.candidates.Count);
                //     string logBuffer = "";
                //     Stack<(int, SpatialGraphNode)> stack = new Stack<(int, SpatialGraphNode)>();
                //     SpatialGraphNode root = sg.root;
                //     stack.Push((1, root));

                //     while(stack.Count != 0){
                //         var top = stack.Pop();
                //         int depth = top.Item1;
                //         SpatialGraphNode node = top.Item2;
                //         for (int j = 0; j < depth; j++) logBuffer += "--";
                //         logBuffer += "[" + node.nodeType + ";" + node.name + ";" + node.label;
                //         if (node.nodeType >= 3) logBuffer += ";" + node.pos + ";" + node.eulerRot;
                //         if (node.nodeType == 3){
                //             logBuffer += ";" + node.poseVerb + ";" + node.furnitureNoun + ";" + node.interactVerb + ";" + node.interactNoun;
                //         }
                //         logBuffer += "]\n";
                //         foreach (SpatialGraphNode son in node.sons) stack.Push((depth + 1, son));
                //     }
                //     Debug.Log(logBuffer);
                // }
                /////////////////////////////////////////////////////////////////////////////////////

                proposedSolnIdx = (solutionsIdxes[toMoveEGIdx].Item1, targets[UnityEngine.Random.Range(0, targets.Count)]);
            }

            timeNow = Time.realtimeSinceStartup;
            //Debug.Log(" It took " + (timeNow - lastInterval) + " to make a proposal.");
            lastInterval = timeNow;

            solutionsIdxes[toMoveEGIdx] = proposedSolnIdx;

            ///////////////////// TEMP ////////////////////////
            // update cost and decide whether to accept the proposal. If not, rollback moved nodes
            //List<LocalSolution> proposal = InstantiateStory(solutionsIdxes);

            timeNow = Time.realtimeSinceStartup;
            //Debug.Log(" It took " + (timeNow - lastInterval) + " to initiate a proposal.");
            lastInterval = timeNow;

            float proposalCost = CalcStroyCost(InstantiateStory(solutionsIdxes));

            timeNow = Time.realtimeSinceStartup;
            //Debug.Log(" It took " + (timeNow - lastInterval) + " to calc proposal cost.");
            lastInterval = timeNow;
            ///////////////////// TEMP ////////////////////////

            float acceptProb = Mathf.Exp((cost - proposalCost) / temper);
            acceptProb = Mathf.Min(1.0f, acceptProb);
            float prob = UnityEngine.Random.Range(0.0f, 1.0f);

            if (prob < acceptProb) {
                cost = proposalCost;
            }
            else{
                // if the proposal is declined, rollback
                solutionsIdxes[toMoveEGIdx] = currentSolnIdx;
            }

            temper *= dec;
            iterCnt++;
            //break;
        }
        // Debug.Log(iterCnt + " iters during MCMC.");

        //UpdateTreesInfo(solutionsIdxes, spatialGraphs);
        return solutionsIdxes;
    }

    public void DebugNeighborMask(int curEGIdx){ 
        HashSet<string> possibleFurnitureNouns = new HashSet<string>();
        List<furnitureInstance> possibleFurnitureInstances = new List<furnitureInstance>();
        EventGraph eg = eventGraphList[curEGIdx];
        string playCharacterRoomName = "";

        foreach (var sg in eg.spatialGraphCandidates){
            SpatialGraphNode character = sg.name2character[playerCharacterName];
            possibleFurnitureNouns.Add(character.furnitureNoun);

            if (playCharacterRoomName == "") playCharacterRoomName = character.father.father.name;
        }

        foreach (var furnitureNoun in possibleFurnitureNouns){
            // if (!room2label2furnitureList.ContainsKey(playCharacterRoomName)){
            //     Debug.Log(playCharacterRoomName + " not exists.");
            // }
            // if (!room2label2furnitureList[playCharacterRoomName].ContainsKey(furnitureNoun)){
            //     Debug.Log(playCharacterRoomName + ", " + furnitureNoun + " not exists.");
            // }
            foreach (var furnitureInstance in room2label2furnitureList[playCharacterRoomName][furnitureNoun]){
                possibleFurnitureInstances.Add(furnitureInstance);
            }
        }

        // each furniture instance corresponds to a user action
        // synthesize a story for each action
        foreach (var furnitureInstance in possibleFurnitureInstances){ 
            Vector2 playerCharacterPos = new Vector2(furnitureInstance.x, furnitureInstance.z);

            // Debug.Log("///////////////////////////////////////////////////////////////////////////");
            // Debug.Log("For a selection of " + furnitureInstance.label + " at " + playerCharacterPos);

            float totalProb = 0.0f;

            // build candidate masks to temporarily disable candidate solutions that do not fit the current user action
            List<int> bannedSGIdxes = new List<int>();
            //foreach (var sg in eg.spatialGraphCandidates){
            for (int i = 0; i < eg.spatialGraphCandidates.Count; i++){
                SpatialGraph sg = eg.spatialGraphCandidates[i];
                sg.candidatesMasks = new List<bool>();
                bool hasActiveCandidates = false;
                foreach (var candidateSoln in sg.candidates){
                    bool mask = false;
                    Vector3 thisPlayerCharacterPosVec3 = candidateSoln.nodes[candidateSoln.FindeNodeIdxByName(playerCharacterName)].pos;
                    Vector2 thisPlayerCharacterPos = new Vector2(thisPlayerCharacterPosVec3.x, thisPlayerCharacterPosVec3.z);
                    if (Vector2.Distance(playerCharacterPos, thisPlayerCharacterPos) < 0.001f) {
                        mask = true;
                        hasActiveCandidates = true;

                        // Debug.Log(playerCharacterName + " at " + candidateSoln.nodes[candidateSoln.FindeNodeIdxByName(playerCharacterName)].pos);
                    }
                    sg.candidatesMasks.Add(mask);
                }

                if (hasActiveCandidates){
                    totalProb += sg.prob;
                    sg.BuildCandidatesKNearestNeighbors(10);
                }
                else bannedSGIdxes.Add(i);
            }

            // update the probability model on SGs
            if (1.0f - totalProb > 1e-7){
                float scale = 1.0f / totalProb;

                for (int i = 0; i < eg.spatialGraphCandidates.Count; i++){
                    //Debug.Log(eg.spatialGraphCandidates[i].prob);
                    if (bannedSGIdxes.Contains(i)) eg.spatialGraphCandidates[i].dynamicProb = 0.0f;
                    else eg.spatialGraphCandidates[i].dynamicProb = eg.spatialGraphCandidates[i].prob * scale;
                }
            }

            // step 1: draw a random prob, select a branch of sg          
            int sgIdx = eg.RandomSGBranchIdx();
            SpatialGraph randSG = eg.spatialGraphCandidates[sgIdx];

            // step 2: draw a random candidate solution with its mask true
            List<int> activeCandidateIdxes = new List<int>();
            for (int i = 0; i < randSG.candidates.Count; i++){
                if (randSG.candidatesMasks[i]) activeCandidateIdxes.Add(i);
            }
            int candidateIdx = activeCandidateIdxes[UnityEngine.Random.Range(0, activeCandidateIdxes.Count)];

            List<int> targets = randSG.nearestNeighbors[candidateIdx].ToList();
            int targetSize = targets.Count * 2;

            List<int> outsideNeighborhoodCandidates = new List<int>();
            for (int anotherCandidateIdx = 0; anotherCandidateIdx < randSG.candidates.Count; anotherCandidateIdx++){
                if (anotherCandidateIdx != candidateIdx && 
                    (randSG.candidatesMasks == null || randSG.candidatesMasks[anotherCandidateIdx]) && 
                    !targets.Contains(anotherCandidateIdx)) {
                    outsideNeighborhoodCandidates.Add(anotherCandidateIdx);
                }
            }

            outsideNeighborhoodCandidates.Shuffle();

            foreach (int anotherCandidateIdx in outsideNeighborhoodCandidates){
                if (targets.Count < targetSize) targets.Add(anotherCandidateIdx);
                else break;
            }
        }
    }
}

public class Optimizer : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        StorySolutionSampler sampler = new StorySolutionSampler("B", "apartment.json", 0.8f);
        //sampler.PrepareEventGraphs("apartment.txt");

        sampler.LoadEventGraphs("newtest.txt");

        // Debug.Log("num of events: " + sampler.eventGraphList.Count);
        // Debug.Log("num of parse graphs in event 0: " + sampler.eventGraphList[0].spatialGraphCandidates.Count);
        // Debug.Log("num of candidate solutions in sp 0 of event 0: " + sampler.eventGraphList[0].spatialGraphCandidates[0].candidates.Count);
        // Debug.Log("nodes in this pg:");
        // foreach (MovableNode node in sampler.eventGraphList[0].spatialGraphCandidates[0].candidates[0].nodes){
        //     Debug.Log(node.name + ", " + node.label + ", " + node.pos);
        // }

        Dictionary<string, (int, int)> pos2Indexes = sampler.DymanicStory(0);
        //sampler.DebugNeighborMask(3);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
