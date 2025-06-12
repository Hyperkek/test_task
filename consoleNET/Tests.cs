using Microsoft.EntityFrameworkCore;
using Xunit;

public class BoxValidationTests
{
    [Theory]
    [InlineData(0, 10, 10, 100, "2023-01-01", null)]
    [InlineData(10, 0, 10, 100, "2023-01-01", null)]
    [InlineData(10, 10, 0, 100, "2023-01-01", null)]
    [InlineData(10, 10, 10, 0, "2023-01-01", null)]
    public void Box_CannotHaveZeroValues_ThrowsException(uint w, uint h, uint d, uint weight, string prodDate, string expDate) //тест на нулевые значения 
    {
        var productionDate = prodDate != null ? DateOnly.Parse(prodDate) : (DateOnly?)null;
        var expireDate = expDate != null ? DateOnly.Parse(expDate) : (DateOnly?)null;

        Assert.Throws<ArgumentException>(() =>
            new Box(w, h, d, weight, productionDate, expireDate));
    }

    [Fact]
    public void Box_ExpireDateMustBeAfterProductionDate_ThrowsException() // тест на срок годности после даты производства
    {
        var productionDate = new DateOnly(2023, 1, 2);
        var expireDate = new DateOnly(2023, 1, 1);

        var ex = Assert.Throws<ArgumentException>(() =>
            new Box(10, 10, 10, 100, productionDate, expireDate));

        Assert.Contains("Дата истечения срока годности должна быть позже даты производства", ex.Message);
    }
}

public class PalletValidationTests
{
    [Fact]
    public void CannotAddBoxLargerThanPallet_ThrowsException() // тест что коробка не может быть больше паллета
    {
        var pallet = new Pallet(100, 100, 100);
        var box = new Box(101, 100, 100, 100, new DateOnly(2023, 1, 1), null);

        var ex = Assert.Throws<InvalidOperationException>(() => pallet.AddBox(box));
        Assert.Contains("Коробка не может быть больше паллета", ex.Message);
    }

    [Fact]
    public void CannotAddSameBoxTwice_ThrowsException() // тест что коробка уже в паллете
    {
        var pallet = new Pallet(100, 100, 100);
        var box = new Box(50, 50, 50, 100, new DateOnly(2023, 1, 1), null);

        pallet.AddBox(box);

        var ex = Assert.Throws<InvalidOperationException>(() => pallet.AddBox(box));
        Assert.Contains("уже добавлена в этот паллет.", ex.Message);
    }

    [Fact]
    public void CannotAddBoxToMultiplePallets_ThrowsException() // тест что коробка уже в другом паллете
    {
        var pallet1 = new Pallet(100, 100, 100);
        var pallet2 = new Pallet(100, 100, 100);
        var box = new Box(50, 50, 50, 100, new DateOnly(2023, 1, 1), null);

        pallet1.AddBox(box);

        var ex = Assert.Throws<InvalidOperationException>(() => pallet2.AddBox(box));
        Assert.Contains("уже находится в другом паллете", ex.Message);
    }
}

public class DateValidationTests
{
    [Fact]
    public void Box_WithProductionDateOnly_SetsCorrectExpireDate() // тест рассчета корректной даты истечения при null значении
    {
        var productionDate = new DateOnly(2023, 1, 1);
        var expectedExpireDate = productionDate.AddDays(100);

        var box = new Box(10, 10, 10, 100, productionDate, null);

        Assert.Equal(expectedExpireDate, box.ExpireDate);
    }

    [Fact]
    public void Box_WithExpireDateOnly_KeepsOriginalExpireDate() // тест корректности конструктора коробки
    {
        var expireDate = new DateOnly(2023, 6, 1);

        var box = new Box(10, 10, 10, 100, null, expireDate);

        Assert.Equal(expireDate, box.ExpireDate);
        Assert.Null(box.ProductionDate);
    }

    [Fact]
    public void Pallet_ExpireDate_EqualsMinBoxExpireDate() // тест корректности срока истечения для паллеты
    {
        var pallet = new Pallet(100, 100, 100);
        var box1 = new Box(10, 10, 10, 100, null, new DateOnly(2023, 6, 1));
        var box2 = new Box(10, 10, 10, 100, null, new DateOnly(2023, 5, 1));

        pallet.AddBox(box1);
        pallet.AddBox(box2);

        Assert.Equal(box2.ExpireDate, pallet.ExpireDate);
    }
}

public class WarehouseIntegrationTests
{
    private WarehouseContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new WarehouseContext(options);
    }

    [Fact]
    public void Warehouse_CannotSaveBoxWithNullProductionAndExpireDate_ThrowsException() // тест что ни одна дата (производства/годности) не введена и это вызывает корректную ошибку
    {
        using var context = CreateInMemoryContext();
        var warehouse = new Warehouse(context);
        var ex = Assert.Throws<ArgumentException>(() =>
            warehouse.AddBox(new Box(10, 10, 10, 100, null, null)));
        Assert.Equal("Должно быть задано хотя бы одно значение: даты производства или истечения срока годности.", ex.Message);
    }
}