import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
const localApi={target:'http://127.0.0.1:5860',changeOrigin:true,configure:(proxy:any)=>proxy.on('proxyReq',(request:any)=>request.setHeader('origin','http://127.0.0.1:5860'))};
export default defineConfig({plugins:[react()],server:{proxy:{'/api':localApi,'/.well-known':localApi,'/connect':localApi,'/auth/login':localApi,'/hubs':{...localApi,ws:true},'/catalog':localApi,'/mobile':localApi}}});
