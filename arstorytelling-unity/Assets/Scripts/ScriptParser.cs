using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Priority_Queue;

[Serializable]
public class SpatialGraphNode{
    public SpatialGraphNode father;
    public List<SpatialGraphNode> sons;
    public int nodeType; //0: scene root; 1: room; 2: furniture; 3: character; 4: item
    public string name;
    public string label;

    // only available for movable nodes including character and item nodes
    public bool sampleSignal;
    public SerializableVector3 pos;
    public float eulerRot;

    // only available for character nodes

    public float headRotOffset;
    public string poseVerb = null;
    public string furnitureNoun = null;
    public string interactVerb = null;
    public string interactNoun = null;
    public int localFurnitureIdx = -1;

    public SpatialGraphNode(int nodeType, string name, string label, bool sampleSignal = false){
        this.nodeType = nodeType;
        this.name = name;
        this.label = label;
        this.sampleSignal = sampleSignal;

        if (nodeType >= 3){
            pos = new Vector3();
            eulerRot = 0.0f;
            headRotOffset = 0.0f;
        }

        father = null;
        sons = new List<SpatialGraphNode>();
    }

    public void UpdateCharacterInfo(string poseVerb, string furnitureNoun, string interactVerb, string interactNoun){
        this.poseVerb = poseVerb;
        this.furnitureNoun = furnitureNoun;
        this.interactVerb = interactVerb;
        this.interactNoun = interactNoun;
    }

    public SpatialGraphNode CopyNode(bool copySons = true, bool copyCharacters = true){
        SpatialGraphNode copy = new SpatialGraphNode(nodeType, name, label);
        if (nodeType >= 3){
            copy.pos = pos;
            copy.eulerRot = eulerRot;

            if (nodeType == 3) {
                copy.headRotOffset = headRotOffset;
                copy.UpdateCharacterInfo(poseVerb, furnitureNoun, interactVerb, interactNoun);
                copy.localFurnitureIdx = localFurnitureIdx;
            }
        }
        
        if (copySons){
            foreach(SpatialGraphNode son in sons){
                if (!copyCharacters){
                    if (son.nodeType == 2){
                        string[] vals = son.name.Split('_');
                        if (vals.Length == 1 || (vals.Length > 1 && vals[1] != "container")) continue;
                    }
                }
                SpatialGraphNode copiedSon = son.CopyNode(copySons, copyCharacters);
                copiedSon.Attach(copy);
            }
        }

        return copy;
    }

    public void AddSon(SpatialGraphNode son){
        sons.Add(son);
    }

    // Some furniture objects might have the same semantic label but should have unique names, eg. multiple chairs in the scene room
    // This function iterates through sons of a node and generate a unique name by appending a counting number at the end
    public string AutoNewFurnitureName(string newSonLabel){
        string name = newSonLabel;
        int cnt = 0;
        foreach (SpatialGraphNode son in sons){
            if (son.label == newSonLabel){
                string suffix = son.name.Split('_')[1];
                if (suffix != "container"){
                    int idx = int.Parse(suffix);
                    cnt = Mathf.Max(cnt, idx + 1);
                }
            }
        }
        
        name += "_" + cnt;
        return name;
    }

    public void Attach(SpatialGraphNode father){
        father.sons.Add(this);
        this.father = father;
    }

    public void Detach(){
        SpatialGraphNode father = this.father;
        for (int i = 0; i < father.sons.Count; i++){
            if (father.sons[i].name == this.name){
                father.sons.RemoveAt(i);
                break;
            }
        }

        this.father = null;
    }
}

[Serializable]
public class SpatialGraph{
    public SpatialGraphNode root;
    public Dictionary<string, SpatialGraphNode> name2room;
    //public Dictionary<string, SpatialGraphNode> name2furniture;
    public Dictionary<string, SpatialGraphNode> name2character;
    public Dictionary<string, SpatialGraphNode> name2item;
    public float prob;
    public float dynamicProb;
    public List<LocalSolution> candidates;
    public List<bool> candidatesMasks;
    public List<List<int>> nearestNeighbors;

    public SpatialGraph(SpatialGraphNode root){
        this.root = root;

        name2room = new Dictionary<string, SpatialGraphNode>();
        //name2furniture = new Dictionary<string, SpatialGraphNode>();
        name2character = new Dictionary<string, SpatialGraphNode>();
        name2item = new Dictionary<string, SpatialGraphNode>();

        prob = 1.0f;
        dynamicProb = 1.0f;
        //candidates = new List<LocalSolution>();
    }

    public void BuildName2Nodes(SpatialGraphNode cur = null){
        if (cur == null) cur = root;

        if (cur.nodeType == 1) {
            name2room.Add(cur.name, cur);
        }
        else if (cur.nodeType == 3) {
            name2character.Add(cur.name, cur);
        }
        else if (cur.nodeType == 4) {
            name2item.Add(cur.name, cur);
        }

        foreach(var son in cur.sons) {
            BuildName2Nodes(son);
        }
    }
    
    public void BuildCandidatesKNearestNeighbors(int k){
        nearestNeighbors = new List<List<int>>();
        
        for (int i = 0; i < candidates.Count; i++){
            List<int> iNearestNeighbors = new List<int>();
            if (candidatesMasks == null || candidatesMasks[i]){
                SimplePriorityQueue<int> pq = new SimplePriorityQueue<int>();
                for (int j = 0; j < candidates.Count; j++){
                    if (i == j || (candidatesMasks != null && !candidatesMasks[j])) continue;
                    pq.Enqueue(j, candidates[i].Similarity(candidates[j]));
                }

                for (int j = 0; j < k & pq.Count > 0; j++){
                    iNearestNeighbors.Add(pq.Dequeue());
                }
            }
            nearestNeighbors.Add(iNearestNeighbors);
        }
    }
}

[Serializable]
public class EventGraph{
    public List<SpatialGraph> spatialGraphCandidates;
    public int stamp;

    public EventGraph(int stamp){
        this.stamp = stamp;

        spatialGraphCandidates = new List<SpatialGraph>();
    }

    public int RandomSGBranchIdx(){
        float randSGBranchProb = UnityEngine.Random.Range(0.0f, 1.0f);
        //if (spatialGraphCandidates[0].root.name == "root_104") randSGBranchProb = 0.99f;
        float accProb = 0.0f;
        int sgIdx;
        //Debug.Log(spatialGraphCandidates[0].root.name + ", rand prob = " + randSGBranchProb);
        for (sgIdx = 0; sgIdx < spatialGraphCandidates.Count; sgIdx++){
            SpatialGraph sg = spatialGraphCandidates[sgIdx];
            accProb += sg.dynamicProb;
            if (accProb >= randSGBranchProb || 1.0f - accProb < 0.000001f) break;
            //Debug.Log(sg.prob + " -> " +  sg.dynamicProb + "; current acc prob: " + accProb);
        }

        //Debug.Log("idx: " + sgIdx + "/" + spatialGraphCandidates.Count);

        return sgIdx;
    }
}

public class ScriptParser : MonoBehaviour
{
    public string storyScriptName;
    public List<EventGraph> eventGraphList;
    private HashSet<string> itemTypes;
    private HashSet<string> itemTypesInStory;
    private Dictionary<string, string> itemName2Type;
    private SpatialGraph staticSceneStructure;
    private Dictionary<string, Dictionary<string, List<(float, string, string, string, string)>>> activityProb;

    public ScriptParser(string storyScriptName, Dictionary<string, Dictionary<string, List<furnitureInstance>>> room2label2furnitureList){
        this.storyScriptName = storyScriptName;

        Init();

        string[] lines = System.IO.File.ReadAllLines("StoryScripts/" + storyScriptName);
        List<string> treeScripts = new List<string>();
        int stamp = 0;
        for (int i = 0; i < lines.Length; i++){
            string line = lines[i].Split('\n')[0];
            //Debug.Log(line);
            if (line[0] == '@'){
                stamp += int.Parse(line.Split('@')[1]);
            }
            else if (line[0] == '#'){
                eventGraphList.Add(BuildSpatialGraphFromScript(stamp, treeScripts, room2label2furnitureList));
                treeScripts.Clear();
            }
            else{
                treeScripts.Add(line);
            }
        }

        LogSpatialGraphs();
    }

    void Start()
    {
        // Init();

        // //string jsonString  = System.IO.File.ReadAllText("json/chinese_furniture.json");
        // string jsonString  = System.IO.File.ReadAllText("json/chinese_furniture.json");  
        // //Debug.Log(jsonString);
        // furnitureInstance[] furnitureInstanceList = JsonHelper.FromJson<furnitureInstance>(jsonString);

        // Dictionary<string, Dictionary<string, List<furnitureInstance>>> room2label2furnitureList = new Dictionary<string, Dictionary<string, List<furnitureInstance>>>(); 
        // foreach (furnitureInstance instance in furnitureInstanceList){
        //     string roomName = instance.room;
        //     string furnitureLabel = instance.label;
        //     if (instance.is_container > 0) furnitureLabel += " container";

        //     if (!room2label2furnitureList.ContainsKey(roomName)) room2label2furnitureList.Add(roomName, new Dictionary<string, List<furnitureInstance>>());
        //     if (!room2label2furnitureList[roomName].ContainsKey(furnitureLabel)) room2label2furnitureList[roomName].Add(furnitureLabel, new List<furnitureInstance>());
        //     room2label2furnitureList[roomName][furnitureLabel].Add(instance);
        //     //Debug.Log(instance.label + ", " + instance.room + ", " + instance.is_container);
        // }

        // string[] lines = System.IO.File.ReadAllLines("StoryScripts/" + storyScriptName);
        // List<string> treeScripts = new List<string>();
        // int stamp = 0;
        // for (int i = 0; i < lines.Length; i++){
        //     string line = lines[i].Split('\n')[0];
        //     if (line[0] == '@'){
        //         stamp += int.Parse(line.Split('@')[1]);
        //     }
        //     else if (line[0] == '#'){
        //         eventGraphList.Add(BuildSpatialGraphFromScript(stamp, treeScripts, room2label2furnitureList));
        //         treeScripts.Clear();
        //     }
        //     else{
        //         treeScripts.Add(line);
        //     }
        // }

        // LogSpatialGraphs();
    }

    public void LogSpatialGraphs(string fName = "scene_tree_debug_log.txt"){
        string logBuffer = "";
        Stack<(int, SpatialGraphNode)> stack = new Stack<(int, SpatialGraphNode)>();
        foreach (var eg in eventGraphList){
            logBuffer += "****************************** @" + eg.stamp + " ******************************\n";
            for (int i = 0; i < eg.spatialGraphCandidates.Count; i++){
                SpatialGraph sg = eg.spatialGraphCandidates[i];
                SpatialGraphNode root = sg.root;
                logBuffer += "///////////////////// Candidate SG #" + i + ", prob = " + sg.prob + " /////////////////////\n";
                stack.Push((1, root));

                while(stack.Count != 0){
                    var top = stack.Pop();
                    int depth = top.Item1;
                    SpatialGraphNode node = top.Item2;
                    for (int j = 0; j < depth; j++) logBuffer += "--";
                    logBuffer += "[" + node.nodeType + ";" + node.name + ";" + node.label;
                    if (node.nodeType >= 3) logBuffer += ";" + node.pos + ";" + node.eulerRot;
                    if (node.nodeType == 3){
                        logBuffer += ";" + node.poseVerb + ";" + node.furnitureNoun + ";" + node.interactVerb + ";" + node.interactNoun;
                    }
                    logBuffer += "]\n";
                    foreach (SpatialGraphNode son in node.sons) stack.Push((depth + 1, son));
                }

                logBuffer += "////////////////////////////////////////////\n";
            }
            logBuffer += "************************************************************\n";
        }

        System.IO.File.WriteAllText("SavedTrees/" + fName, logBuffer);
    }

    EventGraph BuildSpatialGraphFromScript(int stamp, List<string> treeScripts, Dictionary<string, Dictionary<string, List<furnitureInstance>>> room2label2furnitureList){   
        EventGraph eg = new EventGraph(stamp);
        List<(string, string)> charactersInfo = new List<(string, string)>();   // (character name, room)
        List<(string, string, string, string, string, string)> itemsInfo = new List<(string, string, string, string, string, string)>();    // (item name, item type, room, furniture name, furniture type)
        //(activity prob, pose verb, furniture noun, interact verb, interact noun, carrier name)
        List<List<(float, string, string, string, string)>> characterBranches = new List<List<(float, string, string, string, string)>>();

        if (stamp == 0){
            SpatialGraphNode root = new SpatialGraphNode(0, "root_" + stamp, "root");
            staticSceneStructure = new SpatialGraph(root);        
        }

        foreach(string line in treeScripts){
            Debug.Log(line);
            string [] vals = line.Split('|');
            int configType = int.Parse(vals[0]);
            string [] info = vals[1].Split(',');

            if (configType == 0){
                SpatialGraphNode room = new SpatialGraphNode(1, info[0], info[1]);
                room.Attach(staticSceneStructure.root);
                staticSceneStructure.name2room.Add(info[0], room);
            }
            else if (configType == 1){
                string characterName = info[0];
                string activity = info[1];
                string roomName = info[2];
                string roomType = staticSceneStructure.name2room[roomName].label;

                charactersInfo.Add((characterName, roomName));

                if (activity == "get" || activity == "put"){
                    string poseVerb;
                    string furnitureNoun;
                    string interactVerb; 
                    string interactNoun;

                    if (activity == "get"){
                        poseVerb = "stand";
                        furnitureNoun = info[3];
                        interactVerb = "get"; 
                        interactNoun = "none";

                        string [] toGet = info[4].Split(']')[0].Split('[')[1].Split(';');
                        foreach (string toGetName in toGet){
                            itemsInfo.Add((toGetName, itemName2Type[toGetName], "", "", "", characterName));
                        }
                    }
                    else{
                        poseVerb = "stand";
                        furnitureNoun = info[3];
                        interactVerb = "put"; 
                        interactNoun = "none";

                        string [] toPut = info[4].Split(']')[0].Split('[')[1].Split(';');
                        foreach (string toPutName in toPut){
                            itemsInfo.Add((toPutName, itemName2Type[toPutName], roomName, furnitureNoun + "_container", furnitureNoun, ""));
                        }
                    }

                    List<(float, string, string, string, string)> tmpBranches = new List<(float, string, string, string, string)>();
                    tmpBranches.Add((1.0f, poseVerb, furnitureNoun, interactVerb, interactNoun));
                    characterBranches.Add(tmpBranches);
                }
                else{
                    characterBranches.Add(activityProb[roomType][activity]);
                }
            }
            else if (configType == 2){
                string itemName = info[0];
                string itemType = info[1];
                string roomName = info[2];
                string furnitureType = info[3];
                string furnitureName = furnitureType + "_container";

                itemTypesInStory.Add(itemType);
                itemName2Type.Add(itemName, itemType);
                itemsInfo.Add((itemName, itemType, roomName, furnitureName, furnitureType, ""));
            }
        }

        List<int> cnt = new List<int>();
        for (int i = 0; i < characterBranches.Count; i++){
            cnt.Add(characterBranches[i].Count);
        }

        List<string> allCombinations = new List<string>();
        GenerateAllCombinations(cnt, 0, "", allCombinations);

        float totalProb = 0.0f;

        foreach (string comb in allCombinations){
            string[] indexes = comb.Split(';');
            SpatialGraphNode root = staticSceneStructure.root.CopyNode();
            root.name = "root_" + stamp;
            SpatialGraph sg = new SpatialGraph(root);
            sg.BuildName2Nodes();
            
            Dictionary<string, Dictionary<string, int>> room2labelCnt = new Dictionary<string, Dictionary<string, int>>();
            bool valid = true;
            float sgProb = 1.0f;

            for (int i = 0; i < indexes.Length; i++){
                int branchIdx = int.Parse(indexes[i]);
                string characterName = charactersInfo[i].Item1;
                string roomName = charactersInfo[i].Item2;
                (float, string, string, string, string) activityDetails = characterBranches[i][branchIdx];

                if (room2label2furnitureList[roomName].ContainsKey(activityDetails.Item3) // there exists the required furniture object in the scene
                    && (activityDetails.Item5 == "none"
                        || itemTypesInStory.Contains(activityDetails.Item5) // there exists the required virtual object in the story
                        || room2label2furnitureList[roomName].ContainsKey(activityDetails.Item5) // there exists the required furniture object to interact with in the scene
                       )
                   ) 
                {
                    sgProb *= activityDetails.Item1;

                    // create a new furniture node
                    SpatialGraphNode room = sg.name2room[roomName];
                    SpatialGraphNode furniture = new SpatialGraphNode(2, room.AutoNewFurnitureName(activityDetails.Item3), activityDetails.Item3);
                    furniture.Attach(room);

                    // create a new character node
                    SpatialGraphNode character = new SpatialGraphNode(3, characterName, "character", true);
                    character.UpdateCharacterInfo(activityDetails.Item2, activityDetails.Item3, activityDetails.Item4, activityDetails.Item5);
                    sg.name2character.Add(characterName, character);
                    character.Attach(furniture);

                    // update furniture count for this type
                    if (!room2labelCnt.ContainsKey(roomName)) room2labelCnt.Add(roomName, new Dictionary<string, int>());
                    if (!room2labelCnt[roomName].ContainsKey(activityDetails.Item3)) room2labelCnt[roomName].Add(activityDetails.Item3, 0);
                    room2labelCnt[roomName][activityDetails.Item3] = room2labelCnt[roomName][activityDetails.Item3] + 1;
                    if (room2labelCnt[roomName][activityDetails.Item3] > room2label2furnitureList[roomName][activityDetails.Item3].Count){
                        valid = false;
                        break;
                    }
                }
                else{
                    valid = false;
                    break;
                }
            }

            if (valid){
                for (int i = 0; i < itemsInfo.Count; i++){
                    string itemName = itemsInfo[i].Item1;
                    string itemType = itemsInfo[i].Item2;
                    string roomName = itemsInfo[i].Item3;
                    string furnitureName = itemsInfo[i].Item4;
                    string furnitureType = itemsInfo[i].Item5;
                    string carrierName = itemsInfo[i].Item6;

                    // create a new item node
                    SpatialGraphNode item = new SpatialGraphNode(4, itemName, itemType, true);
                    sg.name2item.Add(itemName, item);

                    if (carrierName != ""){
                        SpatialGraphNode carrier = sg.name2character[carrierName];
                        item.Attach(carrier);
                    }
                    else{
                        SpatialGraphNode room = sg.name2room[roomName];
                        bool needNewFurniture = true;
                        SpatialGraphNode furniture = null;
                        foreach (SpatialGraphNode son in room.sons){
                            if (son.name == furnitureName) {
                                furniture = son;
                                needNewFurniture = false;
                                break;
                            }
                        }
                        if (needNewFurniture){
                            furniture = new SpatialGraphNode(2, furnitureName, furnitureType);
                            furniture.Attach(room);
                        }

                        item.Attach(furniture);
                    }
                }

                totalProb += sgProb;
                sg.prob = sgProb;
                sg.dynamicProb = sgProb;
                eg.spatialGraphCandidates.Add(sg);
            }
        }

        // Update the probability since some conditions might not fit the scene and are discarded
        if (1.0f - totalProb > 1e-7){
            float scale = 1.0f / totalProb;

            //Debug.Log(eg.spatialGraphCandidates[0].root.name);

            for (int i = 0; i < eg.spatialGraphCandidates.Count; i++){
                //Debug.Log(eg.spatialGraphCandidates[i].prob + " -> " + eg.spatialGraphCandidates[i].prob * scale);
                eg.spatialGraphCandidates[i].prob *= scale;
                eg.spatialGraphCandidates[i].dynamicProb = eg.spatialGraphCandidates[i].prob;
            }
        }

        //Debug.Log("stamp " + stamp + ": " + eg.spatialGraphCandidates.Count + " branches");
        
        return eg;
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

    void Init(){
        eventGraphList = new List<EventGraph>();

        // a hashset that saves all item types to distinguish from furniture objects
        itemTypes = new HashSet<string>();
        itemTypesInStory = new HashSet<string>();
        itemName2Type = new Dictionary<string, string>();

        itemTypes.Add("dish");
        itemTypes.Add("laptop");
        itemTypes.Add("coffee");
        itemTypes.Add("book");
        itemTypes.Add("mobilephone");
        // itemTypes.Add("coke");

        // init activity probabilities according to learned params. Hard coded for now.
        activityProb = new Dictionary<string, Dictionary<string, List<(float, string, string, string, string)>>>();

        string[] lines = System.IO.File.ReadAllLines("Priors/priors.csv");
        Dictionary<string, List<(float, string, string, string, string)>> roomProb = null;
        List<(float, string, string, string, string)> roomActivityProb = null;
        
        string roomName = "";
        string activityName = "";
        for (int i = 0; i < lines.Length; i++){
            string line = lines[i].Split('\n')[0];
            //Debug.Log(line);

            if (line[0] == '@'){
                // stamp += int.Parse(line.Split('@')[1]);
                roomName = line.Split('@')[1];
                roomProb = new Dictionary<string, List<(float, string, string, string, string)>>();
            }
            else if (line[0] == '#'){
                activityProb[roomName] = roomProb;
            }
            else if (line[0] == '$'){
                activityName = line.Split('$')[1];
                roomActivityProb = new List<(float, string, string, string, string)>();
            }
            else if (line[0] == '&'){
                roomProb.Add(activityName, roomActivityProb);
            }

            else{
                string[] vals = line.Split(',');
                roomActivityProb.Add((float.Parse(vals[0]), vals[1], vals[2], vals[3], vals[4]));
            }
        }
    }
}
