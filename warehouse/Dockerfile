FROM python:3.12-bookworm
WORKDIR /
COPY requirements.txt .
RUN python3 -m pip install --requirement requirements.txt
ENTRYPOINT [ "python3", "/run.py" ]