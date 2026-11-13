using SmartCampus.Api;
using SmartCampus.Domain;
using Xunit;

namespace SmartCampus.Tests;

public sealed class DirectoryTests
{
    [Fact]
    public void Location_tree_groups_children_by_parent_and_does_not_cross_faculty_boundaries()
    {
        var faculty = new CampusLocation { Id = Guid.NewGuid(), Kind = "faculty", Ar = "الكلية", En = "Faculty" };
        var building = new CampusLocation { Id = Guid.NewGuid(), Kind = "building", ParentId = faculty.Id, Ar = "المبنى", En = "Building" };
        var floor = new CampusLocation { Id = Guid.NewGuid(), Kind = "floor", ParentId = building.Id, Ar = "الدور", En = "Floor" };
        var secondFaculty = new CampusLocation { Id = Guid.NewGuid(), Kind = "faculty", Ar = "كلية أخرى", En = "Other faculty" };
        var secondBuilding = new CampusLocation { Id = Guid.NewGuid(), Kind = "building", ParentId = secondFaculty.Id, Ar = "مبنى آخر", En = "Other building" };
        var unattachedFloor = new CampusLocation { Id = Guid.NewGuid(), Kind = "floor", ParentId = Guid.NewGuid(), Ar = "دور يتيم", En = "Unattached floor" };

        var tree = DirectoryController.BuildLocationTree([faculty, building, floor, secondFaculty, secondBuilding, unattachedFloor]);

        Assert.Equal(2, tree.Count);
        var first = Assert.Single(tree, entry => entry.Id == faculty.Id);
        var firstBuilding = Assert.Single(first.Buildings);
        Assert.Equal(building.Id, firstBuilding.Id);
        Assert.Equal(floor.Id, Assert.Single(firstBuilding.Floors).Id);
        var second = Assert.Single(tree, entry => entry.Id == secondFaculty.Id);
        Assert.Equal(secondBuilding.Id, Assert.Single(second.Buildings).Id);
        Assert.Empty(Assert.Single(second.Buildings).Floors);
    }

    [Fact]
    public void Location_tree_returns_empty_collection_without_locations()
    {
        Assert.Empty(DirectoryController.BuildLocationTree([]));
    }
}
