using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public abstract class ThreeDObject // класс трёхмерного объекта от которого наследуются поля классов коробки и паллеты
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public uint Id { get; protected set; }

    [Required]
    [Range(1, uint.MaxValue)]
    public uint Width { 
        get; 
        protected set;
    } // в базе габариты коробок и паллетов хранятся в сантиметрах в uint.

    [Required]
    [Range(1, uint.MaxValue)]
    public uint Height { 
        get; 
        protected set; 
    }

    [Required]
    [Range(1, uint.MaxValue)]
    public uint Depth { 
        get; 
        protected set; 
    }

    [Required]
    [Range(1, uint.MaxValue)]
    public virtual uint Weight { 
        get; 
        protected set; 
    } // вес хранится в граммах в uint, это позволяет избежать отрицательных значений.

    [NotMapped]
    public virtual uint Volume { 
        get; 
        protected set; 
    }
}

public class Box : ThreeDObject // класс коробки
{
    [NotMapped]
    public override uint Volume => Width * Height * Depth;
    public DateOnly? ProductionDate { 
        get; 
        private set; 
    }
    public DateOnly ExpireDate { 
        get; 
        private set; 
    }
    public uint? PalletId { 
        get; 
        private set; 
    }
    public Pallet? Pallet { 
        get; 
        private set; 
    }

    protected Box() { }

    public Box(uint width, uint height, uint depth, uint weight, DateOnly? productionDate, DateOnly? expireDate)
    {
        if (width == 0 || height == 0 || depth == 0 || weight == 0)
            throw new ArgumentException("Габариты коробки должны быть больше нуля.");

        Width = width;
        Height = height;
        Depth = depth;
        Weight = weight;

        switch (productionDate, expireDate)
        {
            case (null, null):
                throw new ArgumentException("Должно быть задано хотя бы одно значение: даты производства или истечения срока годности.");

            case (DateOnly prod, null):
                ProductionDate = prod;
                ExpireDate = prod.AddDays(100);
                break;

            case (null, DateOnly expire):
                ExpireDate = expire;
                break;

            case (DateOnly prod, DateOnly expire):
                if (expire <= prod)
                {
                    throw new ArgumentException("Дата истечения срока годности должна быть позже даты производства.");
                }
                ExpireDate = expire;
                ProductionDate = prod;
                break;
        }
    }
    
    public bool IsExpired()
    {
        return this.ExpireDate > DateOnly.FromDateTime(DateTime.Now);
    }

    public void AssignToPallet(Pallet pallet)
    {
        if (PalletId != null && !ReferenceEquals(Pallet, pallet))
        {
            throw new InvalidOperationException($"Коробка {this.Id} уже находится в другом паллете.");
        }
        Pallet = pallet;
        PalletId = pallet.Id;
    }

    public void RemoveFromPallet()
    {
        Pallet = null;
        PalletId = null;
    }
}

public class Pallet : ThreeDObject // класс паллеты
{
    private readonly List<Box> _boxes = new();
    public IReadOnlyCollection<Box> Boxes => _boxes.AsReadOnly();

    [NotMapped]
    public DateOnly ExpireDate => Boxes.Any() ? _boxes.Min(b => b.ExpireDate) : DateOnly.MaxValue;

    [NotMapped]
    public override uint Volume => Width * Height * Depth + (uint)_boxes.Sum(b => b.Volume);

    [NotMapped]
    public override uint Weight => (uint)_boxes.Sum(b => b.Weight) + 30000;

    protected Pallet() { }

    public Pallet(uint width, uint height, uint depth)
    {
        if (width == 0 || height == 0 || depth == 0) 
        {
            throw new ArgumentException("Габариты паллета должны быть больше нуля.");
        }
        Width = width;
        Height = height;
        Depth = depth;
    }

    public void AddBox(Box box)
    {
        if (box.Width > Width || box.Depth > Depth)
        {
            throw new InvalidOperationException("Коробка не может быть больше паллета.");
        }

        if (box.PalletId != null && !ReferenceEquals(box.Pallet, this))
        {
            throw new InvalidOperationException($"Коробка {box.Id} уже находится в другом паллете.");
        }

        if (_boxes.Contains(box))
        {
            throw new InvalidOperationException($"Коробка {box.Id} уже добавлена в этот паллет.");
        }

        _boxes.Add(box);
        box.AssignToPallet(this);
    }

    public void RemoveBox(Box box)
    {
        if (_boxes.Remove(box))
        {
            box.RemoveFromPallet();
        }
    }

    public bool IsExpired()
    {
        return this.ExpireDate > DateOnly.FromDateTime(DateTime.Now);
    }
}

public class WarehouseContext : DbContext
{
    public DbSet<Pallet> Pallets { 
        get; 
        set; 
    }
    public DbSet<Box> Boxes {
        get; 
        set; 
    }

    public WarehouseContext() { }

    public WarehouseContext(DbContextOptions<WarehouseContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            string projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\.."));
            string dbPath = Path.Combine(projectDir, "warehouse.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Box>().ToTable("Boxes");
        modelBuilder.Entity<Pallet>().ToTable("Pallets");

        modelBuilder.Entity<Box>()
            .HasIndex(b => b.Id)
            .IsUnique();

        modelBuilder.Entity<Pallet>()
            .HasIndex(p => p.Id)
            .IsUnique();

        modelBuilder.Entity<Pallet>()
            .HasMany(p => p.Boxes)
            .WithOne(b => b.Pallet)
            .HasForeignKey(b => b.PalletId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Box>()
            .Property(b => b.ProductionDate)
            .HasConversion(
                d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                d => d.HasValue ? DateOnly.FromDateTime(d.Value) : (DateOnly?)null);

        modelBuilder.Entity<Box>()
            .Property(b => b.ExpireDate)
            .HasConversion(
                d => d.ToDateTime(TimeOnly.MinValue),
                d => DateOnly.FromDateTime(d));
    }
}

public class Warehouse
{
    private readonly WarehouseContext _context;
    public WarehouseContext Context => _context;

    public Warehouse(WarehouseContext context)
    {
        _context = context;
        _context.Database.EnsureCreated();
    }

    public void AddPallet(Pallet pallet)
    {
        _context.Pallets.Add(pallet);
        _context.SaveChanges();
    }

    public void AddBox(Box box)
    {
        _context.Boxes.Add(box);
        _context.SaveChanges();
    }

    public IEnumerable<IGrouping<DateOnly, Pallet>> GetPalletsGroupedByExpiration()
    {
        return _context.Pallets
            .Include(p => p.Boxes)
            .AsEnumerable()
            .OrderBy(p => p.ExpireDate)
            .ThenBy(p => p.Weight)
            .GroupBy(p => p.ExpireDate);
    }

    public IEnumerable<Pallet> GetTop3PalletsWithLongestExpiringBoxes()
    {
        return _context.Pallets
            .Include(p => p.Boxes)
            .Where(p => p.Boxes.Any())
            .AsEnumerable()
            .OrderByDescending(p => p.ExpireDate)
            .Take(3)
            .OrderBy(p => p.Volume);
    }

    public IEnumerable<Box> GetAllBoxes()
    {
        return _context.Boxes.AsEnumerable();
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        try
        {
            using var context = new WarehouseContext();

            // если базы нет то мы ее создаем, если есть то очищаем (закомментировано так как мы читаем из уже созданной базы)
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            var warehouse = new Warehouse(context);

            // заполнение базы тестовыми данными (закомментировано так как мы читаем из уже созданной базы)
            CreateTestData(warehouse);

            // вывод данных из базы данных согласно ТЗ
            PrintResults(warehouse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
            }
            Console.WriteLine("\nПодробности ошибки:");
            Console.WriteLine(ex.ToString());
        }
    }
    private static void CreateTestData(Warehouse warehouse)
    {
        using var transaction = warehouse.Context.Database.BeginTransaction();

        try
        {
            var boxes = new List<Box>
            {
                new Box(10, 50, 20, 100, new DateOnly(2023, 11, 15), null),
                new Box(10, 50, 20, 100, null, new DateOnly(2024, 1, 1)),
                new Box(100, 50, 20, 100, new DateOnly(2023, 11, 15), new DateOnly(2024, 2, 20)),
                new Box(80, 40, 20, 200, new DateOnly(2023, 12, 1), null),
                new Box(80, 40, 20, 4000, new DateOnly(2023, 12, 1), null),
                new Box(80, 40, 20, 200, new DateOnly(2023, 12, 1), null),
                new Box(12, 60, 30, 500, new DateOnly(2023, 10, 10), new DateOnly(2024, 3, 15)),
                new Box(150, 70, 40, 800, null, new DateOnly(2024, 4, 1)),
                new Box(90, 90, 90, 1200, new DateOnly(2023, 12, 25), null),
                new Box(70, 50, 30, 600, new DateOnly(2023, 11, 30), new DateOnly(2024, 1, 15)),
                new Box(10, 10, 50, 1500, new DateOnly(2023, 12, 10), null),
                new Box(60, 40, 20, 300, new DateOnly(2023, 12, 5), new DateOnly(2024, 2, 28)),
                new Box(50, 50, 50, 700, null, new DateOnly(2024, 3, 10)),
                new Box(80, 60, 40, 1000, new DateOnly(2023, 11, 20), null),
                new Box(120, 80, 60, 2000, new DateOnly(2023, 12, 15), new DateOnly(2024, 4, 5)),
                new Box(70, 70, 70, 900, new DateOnly(2023, 12, 20), null),
                new Box(90, 50, 30, 400, new DateOnly(2023, 11, 25), new DateOnly(2024, 1, 31)),
                new Box(110, 60, 40, 800, null, new DateOnly(2023, 12, 5)),
                new Box(100, 80, 50, 1200, new DateOnly(2023, 12, 5), null),
                new Box(60, 60, 60, 600, new DateOnly(2023, 12, 18), new DateOnly(2024, 3, 20))
            };

            foreach (var box in boxes)
            {
                warehouse.Context.Boxes.Add(box);
            }
            warehouse.Context.SaveChanges();

            var pallets = new List<Pallet>
            {
                new Pallet(100, 100, 100),
                new Pallet(100, 100, 100),
                new Pallet(120, 120, 120),
                new Pallet(120, 120, 120),
                new Pallet(150, 150, 150),
                new Pallet(120, 120, 120),
                new Pallet(100, 100, 100),
                new Pallet(120, 120, 120),
                new Pallet(150, 150, 150),
                new Pallet(100, 100, 100),
                new Pallet(120, 120, 120),
                new Pallet(150, 150, 150)
            };

            foreach (var pallet in pallets)
            {
                warehouse.Context.Pallets.Add(pallet);
            }
            warehouse.Context.SaveChanges();

            pallets[0].AddBox(boxes[4]);
            pallets[0].AddBox(boxes[5]);
            pallets[1].AddBox(boxes[2]);
            pallets[2].AddBox(boxes[3]);
            pallets[3].AddBox(boxes[0]);
            pallets[3].AddBox(boxes[1]);
            pallets[4].AddBox(boxes[6]);
            pallets[4].AddBox(boxes[7]);
            pallets[5].AddBox(boxes[8]);
            pallets[6].AddBox(boxes[9]);
            pallets[6].AddBox(boxes[10]);
            pallets[7].AddBox(boxes[11]);
            pallets[7].AddBox(boxes[12]);
            pallets[8].AddBox(boxes[13]);
            pallets[8].AddBox(boxes[14]);
            pallets[9].AddBox(boxes[15]);
            pallets[10].AddBox(boxes[16]);
            pallets[10].AddBox(boxes[17]);
            pallets[11].AddBox(boxes[18]);
            pallets[11].AddBox(boxes[19]);

            warehouse.Context.SaveChanges();

            transaction.Commit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при создании тестовых данных: {ex.Message}");
            throw;
        }
    }

    private static void PrintResults(Warehouse warehouse) // данные при выводе переводятся в метры кубические и киллограмы
    {
        try
        {
            Console.WriteLine("Группировка по дате истечения срока годности (по возрастанию веса в группах):");
            foreach (var group in warehouse.GetPalletsGroupedByExpiration())
            {
                Console.WriteLine($"Срок годности до: {group.Key:yyyy-MM-dd}");
                foreach (var pallet in group)
                {
                    double weightKg = pallet.Weight / 1e3;
                    double volumeM = pallet.Volume / 1e6;
                    Console.WriteLine($"  Паллет {pallet.Id}, Вес: {weightKg}кг, Объём: {volumeM}м^3");
                }
            }

            Console.WriteLine("\nТоп 3 паллеты с самым долгим сроком хранения (по возрастанию объёма):");
            foreach (var pallet in warehouse.GetTop3PalletsWithLongestExpiringBoxes())
            {
                double volumeM = pallet.Volume / 1e6;
                Console.WriteLine($"Паллет {pallet.Id}, Объём: {volumeM}м^3, " +
                                  $"Срок годности до: {pallet.Boxes.Max(b => b.ExpireDate):yyyy-MM-dd}");
            }

            //Console.WriteLine("\nСписок всех коробок:");
            //var boxes = warehouse.GetAllBoxes();
            //foreach (var box in boxes)
            //{
            //    Console.WriteLine($"Коробка {box.Id}:  Вес: {box.Weight} Объём: {box.Volume}");
            //}
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при выводе результатов: {ex.Message}");
            throw;
        }
    }
}
