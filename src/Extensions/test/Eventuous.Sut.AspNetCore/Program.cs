using System.Text.Json;
using Eventuous.Sut.AspNetCore;
using Eventuous.Sut.Domain;
using Microsoft.AspNetCore.Http.Json;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using BookingService = Eventuous.Sut.AspNetCore.BookingService;

DefaultEventSerializer.SetDefaultSerializer(
    new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    )
);

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationService<BookingService, Booking>();

builder.Services.Configure<JsonOptions>(
    options => options.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
);

var app = builder.Build();

app.MapAggregateCommands<Booking>()
    .MapCommand<BookRoom>((cmd, _) => cmd with { GuestId = TestData.GuestId });

app.Run();
