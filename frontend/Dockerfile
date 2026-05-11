FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS final
RUN apk add --no-cache gettext
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/conf.d/nginx.conf.template
EXPOSE 80
CMD ["/bin/sh", "-c", "envsubst '$API_URL' < /etc/nginx/conf.d/nginx.conf.template > /etc/nginx/conf.d/default.conf && envsubst '$GOOGLE_MAPS_API_KEY' < /usr/share/nginx/html/env.template.js > /usr/share/nginx/html/env.js && nginx -g 'daemon off;'"]
