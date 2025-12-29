FROM node:22-alpine
WORKDIR /app
COPY src/tripshare-web/package.json src/tripshare-web/package-lock.json* ./
RUN npm install
COPY src/tripshare-web/ ./
EXPOSE 5173
CMD ["npm","run","dev"]
