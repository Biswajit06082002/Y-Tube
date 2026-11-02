using Processing.BLL;
using Processing.DAL;
using Processing.Worker.Messaging;
using YTube.Common.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddYTubeRabbitMq(builder.Configuration);
builder.Services.AddProcessingBll();
builder.Services.AddProcessingDal(builder.Configuration);
builder.Services.AddHostedService<VideoUploadedConsumer>();

var host = builder.Build();
host.Run();
