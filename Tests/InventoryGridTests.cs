using System;
public static class InventoryGridTests
{
    static int failures;
    static void Check(bool result, string name) { Console.WriteLine((result ? "PASS: " : "FAIL: ") + name); if (!result) failures++; }
    public static int Main()
    {
        var line = new[] { new GridCell(0,0), new GridCell(1,0) };
        var ell = new[] { new GridCell(0,0), new GridCell(0,1), new GridCell(1,1) };
        var grid = new InventoryGrid<object>(3,2); var a = new object(); var b = new object();
        Check(grid.TryPlace(a,line,1,0,0) && grid.Get(1,0)==a && grid.Get(2,0)==a,"valid placement occupies cells");
        Check(!grid.TryPlace(b,line,2,0,0),"out of bounds rejected");
        Check(!grid.TryPlace(b,line,0,0,0),"overlap rejected");
        Check(!grid.TryPlace(a,line,2,1,0) && grid.Get(1,0)==a && grid.Get(2,0)==a,"failed move preserves original");
        Check(grid.TryPlace(a,line,2,0,1) && grid.Get(2,0)==a && grid.Get(2,1)==a && grid.Get(1,0)==null,"rotation and move replace old cells");
        Check(grid.TryPlace(b,ell,0,0,0) && grid.Get(0,0)==b && grid.Get(0,1)==b && grid.Get(1,1)==b && grid.Get(1,0)==null,"irregular L shape leaves gap free");
        Check(!grid.TryPlace(new object(),new[] {new GridCell(0,0),new GridCell(0,0)},1,0,0),"duplicate footprint rejected");
        Check(grid.Remove(a) && grid.Get(2,0)==null && grid.Get(2,1)==null,"remove clears all cells");
        var narrow = new InventoryGrid<object>(1,2); int x,y,r;
        Check(narrow.TryAutoPlace(a,line,out x,out y,out r) && r==1 && narrow.Get(0,1)==a,"automatic placement tries rotation");
        Check(!narrow.TryAutoPlace(b,line,out x,out y,out r),"full grid refuses new item");
        var rotated = InventoryGrid<object>.Rotate(ell,4);
        Check(rotated[0].X==0 && rotated[0].Y==0 && rotated[2].X==1 && rotated[2].Y==1,"four turns restore L shape");
        return failures == 0 ? 0 : 1;
    }
}
