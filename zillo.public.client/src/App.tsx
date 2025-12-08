import { Button } from "@/components/ui/button"

function App() {
  return (
    <div className="min-h-screen bg-background">
      {/* Navigation */}
      <nav className="border-b">
        <div className="container mx-auto px-4 py-4 flex items-center justify-between">
          <div className="text-2xl font-bold">Zillo</div>
          <div className="flex items-center gap-4">
            <Button variant="outline" asChild>
              <a href="https://dashboard.zillo.app/signin">Sign In</a>
            </Button>
            <Button asChild>
              <a href="https://dashboard.zillo.app/signup">Get Started</a>
            </Button>
          </div>
        </div>
      </nav>

      {/* Hero Section */}
      <section className="container mx-auto px-4 py-32 text-center">
        <h1 className="text-5xl font-bold tracking-tight mb-6">
          Turn payments into your
          <span className="text-primary"> customer marketing engine</span>
        </h1>
        <p className="text-xl text-muted-foreground max-w-2xl mx-auto mb-8">
          Transform every transaction into an opportunity to build lasting customer relationships.
        </p>
        <div className="flex gap-4 justify-center">
          <Button size="lg" asChild>
            <a href="https://dashboard.zillo.app/signup">Get Started</a>
          </Button>
          <Button size="lg" variant="outline" asChild>
            <a href="https://dashboard.zillo.app/signin">Sign In</a>
          </Button>
        </div>
      </section>

      {/* CTA Section */}
      <section className="bg-muted py-24">
        <div className="container mx-auto px-4 text-center">
          <h2 className="text-3xl font-bold mb-4">
            Ready to get started?
          </h2>
          <p className="text-xl text-muted-foreground mb-8 max-w-xl mx-auto">
            Join businesses using Zillo to create lasting customer relationships.
          </p>
          <Button size="lg" asChild>
            <a href="https://dashboard.zillo.app/signup">Get Started Free</a>
          </Button>
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t py-12">
        <div className="container mx-auto px-4">
          <div className="flex flex-col md:flex-row justify-between items-center gap-4">
            <div className="text-xl font-bold">Zillo</div>
            <div className="text-muted-foreground">
              &copy; {new Date().getFullYear()} Zillo. All rights reserved.
            </div>
            <div className="flex gap-6">
              <a href="#" className="text-muted-foreground hover:text-foreground transition-colors">
                Privacy
              </a>
              <a href="#" className="text-muted-foreground hover:text-foreground transition-colors">
                Terms
              </a>
              <a href="#" className="text-muted-foreground hover:text-foreground transition-colors">
                Contact
              </a>
            </div>
          </div>
        </div>
      </footer>
    </div>
  )
}

export default App
