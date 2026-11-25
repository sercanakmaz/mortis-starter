using Boilerplate.Shared.Persistence;
using Moq;

namespace Boilerplate.Tests.TestSupport.Shared.Persistence;

public class UnitOfWorkBuilder
{
    private readonly Mock<IUnitOfWork> _mock = new();

    public static UnitOfWorkBuilder Create() => new();

    public UnitOfWorkBuilder WithRecordedCalls(List<string> calls)
    {
        _mock.Setup(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<string>()))
            .Callback<string>(_ => calls.Add(nameof(IUnitOfWork.BeginTransactionAsync)))
            .Returns(Task.CompletedTask);

        _mock.Setup(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<string>()))
            .Callback<string>(_ => calls.Add(nameof(IUnitOfWork.CommitTransactionAsync)))
            .Returns(Task.CompletedTask);

        _mock.Setup(unitOfWork => unitOfWork.RollbackTransactionAsync())
            .Callback(() => calls.Add(nameof(IUnitOfWork.RollbackTransactionAsync)))
            .Returns(Task.CompletedTask);

        return this;
    }

    public UnitOfWorkBuilder WithHasTransaction(bool hasTransaction)
    {
        _mock.Setup(unitOfWork => unitOfWork.HasTransaction).Returns(hasTransaction);

        return this;
    }

    public UnitOfWorkBuilder WithSaveChangesAsync(Action callback = null)
    {
        _mock.Setup(unitOfWork => unitOfWork.SaveChangesAsync())
            .Callback(() => callback?.Invoke())
            .ReturnsAsync(1);

        return this;
    }

    public IUnitOfWork Build() => _mock.Object;
    public Mock<IUnitOfWork> BuildMock() => _mock;
}
