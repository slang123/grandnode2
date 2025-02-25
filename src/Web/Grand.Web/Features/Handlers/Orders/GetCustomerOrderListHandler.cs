﻿using Grand.Business.Core.Interfaces.Catalog.Prices;
using Grand.Business.Core.Interfaces.Checkout.Orders;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Business.Core.Queries.Checkout.Orders;
using Grand.Domain.Orders;
using Grand.Web.Common.Localization;
using Grand.Web.Features.Models.Orders;
using Grand.Web.Models.Orders;
using MediatR;

namespace Grand.Web.Features.Handlers.Orders;

public class GetCustomerOrderListHandler : IRequestHandler<GetCustomerOrderList, CustomerOrderListModel>
{
    private readonly ICurrencyService _currencyService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IGroupService _groupService;
    private readonly IMediator _mediator;
    private readonly IOrderService _orderService;
    private readonly OrderSettings _orderSettings;
    private readonly IOrderStatusService _orderStatusService;
    private readonly IPriceFormatter _priceFormatter;
    private readonly IEnumTranslationService _enumTranslationService;
    
    public GetCustomerOrderListHandler(
        IDateTimeService dateTimeService,
        IGroupService groupService,
        IMediator mediator,
        IPriceFormatter priceFormatter,
        IOrderStatusService orderStatusService,
        IOrderService orderService,
        ICurrencyService currencyService,
        OrderSettings orderSettings, 
        IEnumTranslationService enumTranslationService)
    {
        _dateTimeService = dateTimeService;
        _groupService = groupService;
        _priceFormatter = priceFormatter;
        _orderStatusService = orderStatusService;
        _orderService = orderService;
        _orderSettings = orderSettings;
        _enumTranslationService = enumTranslationService;
        _currencyService = currencyService;
        _mediator = mediator;
    }

    public async Task<CustomerOrderListModel> Handle(GetCustomerOrderList request, CancellationToken cancellationToken)
    {
        var model = new CustomerOrderListModel();
        await PrepareOrder(model, request);
        return model;
    }

    private async Task PrepareOrder(CustomerOrderListModel model, GetCustomerOrderList request)
    {
        if (request.Command.PageSize <= 0) request.Command.PageSize = _orderSettings.PageSize;
        if (request.Command.PageNumber <= 0) request.Command.PageNumber = 1;
        if (request.Command.PageSize == 0)
            request.Command.PageSize = 10;

        var customerId = string.Empty;
        var ownerId = string.Empty;

        if (!await _groupService.IsOwner(request.Customer))
            customerId = request.Customer.Id;
        else
            ownerId = request.Customer.Id;

        var orders = await _orderService.SearchOrders(
            customerId: customerId,
            ownerId: ownerId,
            storeId: request.Store.Id,
            pageIndex: request.Command.PageNumber - 1,
            pageSize: request.Command.PageSize);

        model.PagingContext.LoadPagedList(orders);


        foreach (var order in orders)
        {
            var status = await _orderStatusService.GetByStatusId(order.OrderStatusId);
            var orderModel = new CustomerOrderListModel.OrderDetailsModel {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                OrderCode = order.Code,
                CustomerEmail = order.BillingAddress?.Email,
                CreatedOn = _dateTimeService.ConvertToUserTime(order.CreatedOnUtc, DateTimeKind.Utc),
                OrderStatusId = order.OrderStatusId,
                OrderStatus = status?.Name,
                PaymentStatus = _enumTranslationService.GetTranslationEnum(order.PaymentStatusId),
                ShippingStatus = _enumTranslationService.GetTranslationEnum(order.ShippingStatusId),
                IsMerchandiseReturnAllowed =
                    await _mediator.Send(new IsMerchandiseReturnAllowedQuery { Order = order }),
                OrderTotal = _priceFormatter.FormatPrice(order.OrderTotal,
                    await _currencyService.GetCurrencyByCode(order.CustomerCurrencyCode))
            };

            model.Orders.Add(orderModel);
        }
    }
}