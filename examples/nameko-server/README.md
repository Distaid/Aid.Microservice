# Nameko (Python) RPC Server Example

This example demonstrates a simple Python RPC microservice built with [Nameko](https://nameko.readthedocs.io/), interoperating with .NET Aid.Microservice clients.

## Service Definition

```python
from nameko.rpc import rpc

class GreetingService:
    name = "greeting_service"

    @rpc
    def hello(self, name):
        return f"Hello, {name}!"
```

## How to Run

1. Make sure RabbitMQ is running (e.g. `localhost:5672`).
2. Install dependencies:
   ```shell
   pip install -r requirements.txt
   ```
3. Run the Nameko service:
   ```shell
   nameko run --config config.yaml server
   ```
4. Call `greeting_service.hello` from .NET using `NamekoProtocol`!
