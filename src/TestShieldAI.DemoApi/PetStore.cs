namespace TestShieldAI.DemoApi;

public sealed class PetStore
{
    private readonly object _gate = new();
    private readonly Dictionary<int, Pet> _pets;
    private int _nextId;

    public PetStore()
    {
        _pets = new Dictionary<int, Pet>
        {
            [1] = new Pet { Id = 1, Name = "Buddy", Species = "dog" },
            [2] = new Pet { Id = 2, Name = "Milo", Species = "cat" },
            [3] = new Pet { Id = 3, Name = "Coco", Species = "bird" }
        };
        _nextId = 4;
    }

    public IReadOnlyList<Pet> List()
    {
        lock (_gate)
        {
            return _pets.Values.OrderBy(pet => pet.Id).ToList();
        }
    }

    public Pet? Find(int id)
    {
        lock (_gate)
        {
            return _pets.TryGetValue(id, out var pet) ? pet : null;
        }
    }

    public Pet Add(string name, string species)
    {
        lock (_gate)
        {
            var pet = new Pet
            {
                Id = _nextId++,
                Name = name.Trim(),
                Species = species.Trim()
            };
            _pets[pet.Id] = pet;
            return pet;
        }
    }
}
