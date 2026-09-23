# Patch / explain-your-solve review service. Stdlib only.
FROM python:3.12-alpine
WORKDIR /app
COPY server.py submit.html review.py ./
ENV REVIEW_QUEUE=/data/queue.jsonl PORT=8080
EXPOSE 8080
CMD ["python3", "server.py"]
