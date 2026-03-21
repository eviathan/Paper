UINode BasicApp() {
  return (
    <Box style={{
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      justifyContent: "center",
      height: "100%",
      background: "#1a1a2e",
      padding: 16,
      borderRadius: 8
    }}>
      <Text style={{
        fontSize: 48,
        color: "#ff6b35",
        paddingBottom: 24
      }}>
        Hello World!
      </Text>
      <Button 
        onClick={() => console.log("Button clicked!")}
        style={{
          background: "#0f3460",
          color: "white",
          padding: "12px 24px",
          borderRadius: 6,
          fontSize: 16
        }}
      >
        Click Me
      </Button>
    </Box>
  );
}
