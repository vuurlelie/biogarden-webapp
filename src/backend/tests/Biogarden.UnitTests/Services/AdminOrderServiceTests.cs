using System.Collections;
using System.Linq.Expressions;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;

namespace Biogarden.UnitTests.Services;

public sealed class AdminOrderServiceTests
{
    private readonly Mock<IAppDbContext> _databaseContext = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);
    private readonly Mock<IEmailService> _emailService = new(MockBehavior.Strict);
    private readonly Mock<IEmailTemplateService> _emailTemplateService = new(MockBehavior.Strict);

    [Fact]
    public async Task UpdateAsync_WhenStatusChanges_QueuesCustomerStatusEmail()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            RowVersion = [0, 0, 0, 0, 0, 0, 0, 1],
            OrderNumber = "BK-TEST-0001",
            CartId = Guid.NewGuid(),
            CustomerName = "Teszt Elek",
            CustomerEmail = "customer@example.com",
            CustomerPhone = "+36 20 123 4567",
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            StockCommitted = true
        };
        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = 1,
            ProductName = "Levendula",
            Unit = UnitOfMeasure.Piece,
            UnitPrice = 1800,
            Quantity = 2,
            LineTotal = 3600
        });
        var orders = CreateMockDbSet([order]);
        var statusMessage = new EmailMessage
        {
            To = "customer@example.com",
            Subject = "Rendelésed állapota frissült"
        };

        _databaseContext.SetupGet(context => context.Orders).Returns(orders.Object);
        var emails = new Mock<IOrderEmailRepository>(MockBehavior.Strict);
        _unitOfWork.SetupGet(candidateUnitOfWork => candidateUnitOfWork.OrderEmailRepository).Returns(emails.Object);
        emails.Setup(orderEmailRepository => orderEmailRepository.Add(It.IsAny<OrderEmail>()));
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _emailTemplateService
            .Setup(service => service.BuildOrderStatusChangedMessage("customer@example.com", It.Is<OrderDto>(updatedOrder => updatedOrder.OrderNumber == order.OrderNumber && updatedOrder.Status == OrderStatus.Confirmed)))
            .Returns(statusMessage);
        _emailService
            .Setup(service => service.SendAsync(statusMessage, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new AdminOrderService(_databaseContext.Object, _unitOfWork.Object, _emailService.Object, _emailTemplateService.Object);

        var result = await service.UpdateAsync(
            order.Id,
            new AdminOrderUpdateDto
            {
                Version = Convert.ToBase64String(order.RowVersion),
                Status = OrderStatus.Confirmed,
                PaymentStatus = PaymentStatus.Pending
            });

        Assert.Equal(OrderStatus.Confirmed, result.Status);
        _emailService.Verify(service => service.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        emails.Verify(
            orderEmailRepository => orderEmailRepository.Add(It.Is<OrderEmail>(email => email.OrderId == order.Id &&
                email.Kind == "status" && email.EventId != Guid.Empty && email.Recipient == statusMessage.To)),
            Times.Once
        );
    }

    private static Mock<DbSet<TEntity>> CreateMockDbSet<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class
    {
        var queryable = entities.AsQueryable();
        var mockSet = new Mock<DbSet<TEntity>>();

        mockSet.As<IAsyncEnumerable<TEntity>>()
            .Setup(set => set.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) => new TestAsyncEnumerator<TEntity>(queryable.GetEnumerator()));

        mockSet.As<IQueryable<TEntity>>()
            .Setup(set => set.Provider)
            .Returns(new TestAsyncQueryProvider<TEntity>(queryable.Provider));
        mockSet.As<IQueryable<TEntity>>().Setup(set => set.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<TEntity>>().Setup(set => set.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<TEntity>>().Setup(set => set.GetEnumerator()).Returns(() => queryable.GetEnumerator());

        return mockSet;
    }

    private sealed class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        public TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return new TestAsyncEnumerable<TEntity>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new TestAsyncEnumerable<TElement>(expression);
        }

        public object? Execute(Expression expression)
        {
            return _inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return _inner.Execute<TResult>(expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])
                ?? throw new InvalidOperationException("IQueryProvider.Execute method was not found.");
            var typedExecutionResult = executionResult
                .MakeGenericMethod(expectedResultType)
                .Invoke(this, [expression]);

            var fromResultMethod = typeof(Task)
                .GetMethod(nameof(Task.FromResult))
                ?? throw new InvalidOperationException("Task.FromResult method was not found.");

            var taskResult = fromResultMethod
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, [typedExecutionResult])
                ?? throw new InvalidOperationException("Task.FromResult invocation returned null.");

            return (TResult)taskResult;
        }
    }

    private sealed class TestAsyncEnumerable<TEntity> :
        EnumerableQuery<TEntity>,
        IAsyncEnumerable<TEntity>,
        IQueryable<TEntity>
    {
        public TestAsyncEnumerable(IEnumerable<TEntity> enumerable)
            : base(enumerable)
        {
        }

        public TestAsyncEnumerable(Expression expression)
            : base(expression)
        {
        }

        public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TestAsyncEnumerator<TEntity>(this.AsEnumerable().GetEnumerator());
        }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<TEntity>(this);
    }

    private sealed class TestAsyncEnumerator<TEntity> : IAsyncEnumerator<TEntity>
    {
        private readonly IEnumerator<TEntity> _inner;

        public TestAsyncEnumerator(IEnumerator<TEntity> inner)
        {
            _inner = inner;
        }

        public TEntity Current => _inner.Current;

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return ValueTask.FromResult(_inner.MoveNext());
        }
    }
}
