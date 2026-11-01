// Run this method body with Unity MCP execute_code in the open project.
// No scene objects, physics, assets, or room data are modified by the simulator.
System.Action<bool,string> check = (ok,message) => { if(!ok) throw new System.Exception(message); };
System.Func<GravityRoguelike.World.RoomData,string> snapshot = room => {
    var s=new System.Text.StringBuilder();
    s.Append(room.Id).Append(room.Seed).Append(room.Bounds);
    for(int y=0;y<room.Height;y++) for(int x=0;x<room.Width;x++)
        s.Append((int)room.GetCell(new Vector2Int(x,y)));
    return s.ToString();
};
System.Action<System.Action,System.Type> rejects = (action,type) => {
    try { action(); }
    catch(System.Exception e) {
        check(e.GetType()==type,"Wrong exception: "+e.GetType());
        return;
    }
    throw new System.Exception("Expected "+type.Name);
};
var directions=new[] {
    GravityRoguelike.World.GravityDirection.Down, GravityRoguelike.World.GravityDirection.Up,
    GravityRoguelike.World.GravityDirection.Left, GravityRoguelike.World.GravityDirection.Right
};
var steps=new[] {new Vector2Int(0,-1),new Vector2Int(0,1),new Vector2Int(-1,0),new Vector2Int(1,0)};
var landings=new[] {new Vector2Int(4,1),new Vector2Int(4,5),new Vector2Int(1,3),new Vector2Int(7,3)};
int checks=0;
System.Action<GravityRoguelike.World.RoomData,GravityRoguelike.World.PlayerState,Vector2Int> verify = (room,start,expected) => {
    string before=snapshot(room);
    var original=start;
    var result=GravityRoguelike.World.GravityMovementSimulator.SimulateGravity(room,start);
    check(result.Position==expected,"Wrong landing for "+start+": "+result);
    check(result.Gravity==start.Gravity,"Gravity changed");
    check(start==original,"Input state changed");
    check(snapshot(room)==before,"Room modified");
    check(result==GravityRoguelike.World.GravityMovementSimulator.SimulateGravity(room,start),"Non-deterministic");
    check(result==GravityRoguelike.World.GravityMovementSimulator.SimulateGravity(room,result),"Not idempotent");
    checks++;
};
for(int d=0;d<4;d++) {
    var room=new GravityRoguelike.World.RoomData(7,new RectInt(100,200,9,7),42);
    GravityRoguelike.World.RoomBorderGenerator.Generate(room);
    var start=new GravityRoguelike.World.PlayerState(new Vector2Int(4,3),directions[d]);
    verify(room,start,landings[d]);
    room.SetCell(start.Position+steps[d]*2,GravityRoguelike.World.CellType.Solid);
    verify(room,start,start.Position+steps[d]); // first internal obstacle, empty space behind it
    room.SetCell(start.Position+steps[d],GravityRoguelike.World.CellType.Solid);
    verify(room,start,start.Position); // immediately supported
}
foreach(var size in new[] {new Vector2Int(1,1),new Vector2Int(1,9),new Vector2Int(9,1),new Vector2Int(2,5),new Vector2Int(6,2)}) {
    var room=new GravityRoguelike.World.RoomData(1,new RectInt(-40,100,size.x,size.y),0);
    // All cells Empty, including the perimeter: the room boundary itself must stop movement.
    for(int y=0;y<size.y;y++) for(int x=0;x<size.x;x++) for(int d=0;d<4;d++) {
        var expected=new Vector2Int(d==2?0:d==3?size.x-1:x,d==0?0:d==1?size.y-1:y);
        verify(room,new GravityRoguelike.World.PlayerState(new Vector2Int(x,y),directions[d]),expected);
    }
}
// Exhaustive 3x3 obstacle layouts; independent nearest-obstacle oracle, no traversal graph.
for(int mask=0;mask<512;mask++) {
    var room=new GravityRoguelike.World.RoomData(8,new RectInt(0,0,3,3),123);
    var shifted=new GravityRoguelike.World.RoomData(99,new RectInt(800,400,3,3),999);
    for(int i=0;i<9;i++) {
        var cell=(mask & (1<<i))!=0 ? GravityRoguelike.World.CellType.Solid : GravityRoguelike.World.CellType.Empty;
        room.SetCell(new Vector2Int(i%3,i/3),cell);
        shifted.SetCell(new Vector2Int(i%3,i/3),cell);
    }
    for(int y=0;y<3;y++) for(int x=0;x<3;x++) {
        var p=new Vector2Int(x,y);
        if(room.GetCell(p)!=GravityRoguelike.World.CellType.Empty) continue;
        for(int d=0;d<4;d++) {
            int distance=d==0?y:d==1?2-y:d==2?x:2-x;
            for(int i=0;i<9;i++) {
                if((mask & (1<<i))==0) continue;
                int dx=i%3-x,dy=i/3-y;
                int along=dx*steps[d].x+dy*steps[d].y;
                int across=dx*steps[d].y-dy*steps[d].x;
                if(across==0 && along>0) distance=System.Math.Min(distance,along-1);
            }
            var start=new GravityRoguelike.World.PlayerState(p,directions[d]);
            verify(room,start,p+steps[d]*distance);
            check(GravityRoguelike.World.GravityMovementSimulator.SimulateGravity(room,start)==
                GravityRoguelike.World.GravityMovementSimulator.SimulateGravity(shifted,start),"Depends on world placement or metadata");
        }
    }
}
var invalidRoom=new GravityRoguelike.World.RoomData(1,new RectInt(0,0,3,3),0);
string untouched=snapshot(invalidRoom);
var valid=new GravityRoguelike.World.PlayerState(new Vector2Int(1,1),directions[0]);
rejects(()=>GravityRoguelike.World.GravityMovementSimulator.SimulateGravity(null,valid),typeof(System.ArgumentNullException));
foreach(var p in new[] {new Vector2Int(-1,0),new Vector2Int(0,-1),new Vector2Int(3,0),new Vector2Int(0,3),new Vector2Int(int.MaxValue,0)}) {
    var invalid=new GravityRoguelike.World.PlayerState(p,directions[0]);
    rejects(()=>GravityRoguelike.World.GravityMovementSimulator.SimulateGravity(invalidRoom,invalid),typeof(System.ArgumentOutOfRangeException));
}
check(snapshot(invalidRoom)==untouched,"Invalid input modified room");
for(int y=0;y<3;y++) for(int x=0;x<3;x++) invalidRoom.SetCell(new Vector2Int(x,y),GravityRoguelike.World.CellType.Solid);
untouched=snapshot(invalidRoom);
rejects(()=>GravityRoguelike.World.GravityMovementSimulator.SimulateGravity(invalidRoom,valid),typeof(System.ArgumentException));
check(snapshot(invalidRoom)==untouched,"Solid-start rejection modified room");
// Public PlayerState construction already rejects invalid enum values.
rejects(()=>{new GravityRoguelike.World.PlayerState(Vector2Int.zero,(GravityRoguelike.World.GravityDirection)99);},typeof(System.ArgumentOutOfRangeException));
var example=new GravityRoguelike.World.RoomData(1,new RectInt(0,0,7,7),0);
GravityRoguelike.World.RoomBorderGenerator.Generate(example);
var down=GravityRoguelike.World.GravityMovementSimulator.SimulateGravity(example,
    new GravityRoguelike.World.PlayerState(new Vector2Int(3,5),directions[0]));
var left=GravityRoguelike.World.GravityMovementSimulator.SimulateGravity(example,
    new GravityRoguelike.World.PlayerState(new Vector2Int(3,5),directions[2]));
check(down.Position==new Vector2Int(3,1) && left.Position==new Vector2Int(1,5),"Gravity-change example");
var turned=GravityRoguelike.World.GravityMovementSimulator.SimulateGravity(example,
    new GravityRoguelike.World.PlayerState(down.Position,directions[2]));
check(turned.Position==new Vector2Int(1,1),"Gravity change after landing");
var set=new System.Collections.Generic.HashSet<GravityRoguelike.World.PlayerState>();
foreach(var g in directions) set.Add(new GravityRoguelike.World.PlayerState(new Vector2Int(3,5),g));
set.Add(new GravityRoguelike.World.PlayerState(new Vector2Int(3,5),directions[0]));
check(set.Count==4,"State equality/hash regression");
return new {result="PASS",movementCases=checks,obstacleLayouts=512,exampleDown=down.ToString(),exampleLeft=left.ToString(),afterTurn=turned.ToString()};

