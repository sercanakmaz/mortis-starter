using RabbitMQ.Client;

namespace Boilerplate.IntegrationTests.TestSupport;

public static class RabbitMqDeliveries
{
    public static async Task<BasicGetResult> NextAsync(IChannel channel, string queue)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var delivery = await channel.BasicGetAsync(queue, autoAck: true);

            if (delivery is not null)
            {
                return delivery;
            }

            await Task.Delay(100);
        }

        return null;
    }
}
